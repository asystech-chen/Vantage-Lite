using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Diagnostics;
using VantageLite.Storage;

namespace VantageLite.Downloads
{
    /// <summary>
    /// 下载历史容器（JSON 根对象）。依赖：无。
    /// </summary>
    [DataContract]
    public sealed class DownloadHistory
    {
        [DataMember(Name = "items", Order = 0)]
        public List<DownloadItem> Items { get; set; }
    }

    /// <summary>
    /// 下载管理：接管 WebView2 DownloadStarting、维护记录、持久化历史。
    /// 依赖：CoreWebView2DownloadOperation / JsonFile / Log。
    /// 改动注意：所有事件都在 UI 线程触发；进度更新不落盘，只在状态变化时保存。
    /// </summary>
    public sealed class DownloadManager
    {
        private const int MaxItems = 200;

        private readonly List<DownloadItem> _items = new List<DownloadItem>();
        private readonly List<RetryRequest> _retryQueue = new List<RetryRequest>();
        private readonly string _historyPath;
        private readonly bool _persist;
        private readonly System.Windows.Forms.Timer _retryTimer;

        private sealed class RetryRequest
        {
            public DownloadItem Item;
            public DateTime QueuedUtc;
        }

        public event EventHandler Changed;
        public event EventHandler<DownloadItem> Started;

        public DownloadManager(string historyPath)
            : this(historyPath, true)
        {
        }

        public DownloadManager(string historyPath, bool persist)
        {
            _historyPath = historyPath;
            _persist = persist;
            _retryTimer = new System.Windows.Forms.Timer();
            _retryTimer.Interval = 5000;
            _retryTimer.Tick += RetryTimer_Tick;

            if (_persist)
            {
                Load();
            }
        }

        public void Dispose()
        {
            _retryTimer.Stop();
            _retryTimer.Dispose();
        }

        public IList<DownloadItem> Items
        {
            get { return _items; }
        }

        /// <summary>
        /// 本次会话产生的下载（含本次重试的历史记录）。弹出面板只用这个。
        /// </summary>
        public IList<DownloadItem> SessionItems
        {
            get
            {
                List<DownloadItem> items = new List<DownloadItem>();

                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].IsSession)
                    {
                        items.Add(_items[i]);
                    }
                }

                return items;
            }
        }

        /// <summary>
        /// 接管一次下载：屏蔽 WebView2 默认下载界面，转由本面板展示。
        /// </summary>
        public DownloadItem Begin(CoreWebView2DownloadStartingEventArgs args)
        {
            if (args == null)
            {
                return null;
            }

            CoreWebView2DownloadOperation operation = args.DownloadOperation;

            if (operation == null)
            {
                return null;
            }

            string filePath = args.ResultFilePath;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                filePath = Path.Combine(DefaultDirectory(), SuggestFileName(operation.Uri));
            }

            // 显式回写保存路径，确保自定义下载目录在没有默认路径时也生效。
            args.ResultFilePath = filePath;

            DownloadItem item = TakeRetry(operation.Uri);
            bool isRetry = item != null;

            if (item == null)
            {
                item = new DownloadItem();
                item.Id = Guid.NewGuid().ToString("N");
                item.Url = operation.Uri;
                item.FilePath = filePath;
                item.FileName = Path.GetFileName(filePath);
                item.BytesReceived = operation.BytesReceived;
                item.TotalBytes = ToLong(operation.TotalBytesToReceive);
                item.State = DownloadItem.StateProgress;
                item.StartedUtc = DateTime.UtcNow;
                item.Operation = operation;
                item.IsSession = true;
                _items.Insert(0, item);
            }
            else
            {
                // 重试复用原记录：不新建任务，原位刷新状态和进度。
                _items.Remove(item);
                _items.Insert(0, item);
                item.Url = operation.Uri;
                item.FilePath = filePath;
                item.FileName = Path.GetFileName(filePath);
                item.BytesReceived = operation.BytesReceived;
                item.TotalBytes = ToLong(operation.TotalBytesToReceive);
                item.State = DownloadItem.StateProgress;
                item.Error = null;
                item.Paused = false;
                item.StartedUtc = DateTime.UtcNow;
                item.Operation = operation;
                item.IsSession = true;
            }

            args.Handled = true;
            args.Cancel = false;

            operation.BytesReceivedChanged += delegate { UpdateProgress(item); };
            operation.StateChanged += delegate { UpdateState(item); };

            // Handled=true 只隐藏 WebView2 默认下载界面，下载会自动开始。
            Save();
            RaiseChanged();

            EventHandler<DownloadItem> started = Started;
            if (!isRetry && started != null)
            {
                started(this, item);
            }

            return item;
        }

        /// <summary>
        /// 标记一条失败/取消记录进入重试：不新建记录，等下一个 DownloadStarting 到来时复用。
        /// </summary>
        public void QueueRetry(DownloadItem item)
        {
            if (item == null)
            {
                return;
            }

            for (int i = _retryQueue.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_retryQueue[i].Item.Id, item.Id, StringComparison.Ordinal))
                {
                    _retryQueue.RemoveAt(i);
                }
            }

            _retryQueue.Add(new RetryRequest { Item = item, QueuedUtc = DateTime.UtcNow });
            _retryTimer.Start();

            item.Operation = null;
            item.Paused = false;
            item.BytesReceived = 0;
            item.TotalBytes = 0;
            item.State = DownloadItem.StateProgress;
            item.Error = null;
            item.StartedUtc = DateTime.UtcNow;
            item.IsSession = true;

            Save();
            RaiseChanged();
            item.RaiseUpdated();
        }

        /// <summary>
        /// 重试启动失败时把记录恢复成中断状态，避免一直显示下载中。
        /// </summary>
        public void FailRetry(DownloadItem item, string error)
        {
            if (item == null)
            {
                return;
            }

            for (int i = _retryQueue.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_retryQueue[i].Item.Id, item.Id, StringComparison.Ordinal))
                {
                    _retryQueue.RemoveAt(i);
                }
            }

            item.State = DownloadItem.StateInterrupted;
            item.Error = error;
            item.Paused = false;
            item.Operation = null;
            StopRetryTimerIfIdle();
            Save();
            RaiseChanged();
            item.RaiseUpdated();
        }

        private DownloadItem TakeRetry(string url)
        {
            DateTime now = DateTime.UtcNow;

            for (int i = _retryQueue.Count - 1; i >= 0; i--)
            {
                if ((now - _retryQueue[i].QueuedUtc).TotalSeconds > 30)
                {
                    _retryQueue.RemoveAt(i);
                }
            }

            for (int i = 0; i < _retryQueue.Count; i++)
            {
                if (string.Equals(_retryQueue[i].Item.Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    DownloadItem item = _retryQueue[i].Item;
                    _retryQueue.RemoveAt(i);
                    StopRetryTimerIfIdle();
                    return item;
                }
            }

            // 重定向可能改变最终 URL：只有一个待重试任务时直接认领它。
            if (_retryQueue.Count == 1)
            {
                DownloadItem item = _retryQueue[0].Item;
                _retryQueue.Clear();
                StopRetryTimerIfIdle();
                return item;
            }

            return null;
        }

        private void StopRetryTimerIfIdle()
        {
            if (_retryQueue.Count == 0)
            {
                _retryTimer.Stop();
            }
        }

        /// <summary>
        /// 重试发起后 30 秒内没有等到 DownloadStarting，恢复为中断，避免一直显示下载中。
        /// </summary>
        private void RetryTimer_Tick(object sender, EventArgs e)
        {
            if (_retryQueue.Count == 0)
            {
                _retryTimer.Stop();
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool changed = false;

            for (int i = _retryQueue.Count - 1; i >= 0; i--)
            {
                if ((now - _retryQueue[i].QueuedUtc).TotalSeconds <= 30)
                {
                    continue;
                }

                DownloadItem item = _retryQueue[i].Item;
                _retryQueue.RemoveAt(i);
                item.State = DownloadItem.StateInterrupted;
                item.Error = "重试超时";
                item.Paused = false;
                item.Operation = null;
                item.RaiseUpdated();
                changed = true;
            }

            if (_retryQueue.Count == 0)
            {
                _retryTimer.Stop();
            }

            if (changed)
            {
                Save();
                RaiseChanged();
            }
        }

        public void Pause(DownloadItem item)
        {
            if (item == null || item.Operation == null || !item.IsActive)
            {
                return;
            }

            try
            {
                // Pause() 会把状态改成 Interrupted(UserCanceled)，先标记 Paused，
                // UpdateState 会忽略暂停期间的原始状态变化。
                item.Paused = true;
                item.State = DownloadItem.StatePaused;
                item.Operation.Pause();
                Save();
                RaiseChanged();
                item.RaiseUpdated();
            }
            catch (Exception ex)
            {
                Log.Warn("[download] pause failed: " + Log.Describe(ex));
            }
        }

        public void Resume(DownloadItem item)
        {
            if (item == null || item.Operation == null || !item.IsActive)
            {
                return;
            }

            try
            {
                item.Paused = false;
                item.State = DownloadItem.StateProgress;
                item.Operation.Resume();
                Save();
                RaiseChanged();
                item.RaiseUpdated();
            }
            catch (Exception ex)
            {
                Log.Warn("[download] resume failed: " + Log.Describe(ex));
            }
        }

        public void Cancel(DownloadItem item)
        {
            if (item == null || item.Operation == null || !item.IsActive)
            {
                return;
            }

            item.State = DownloadItem.StateCancelled;
            item.Paused = false;
            item.Error = "已取消";

            try
            {
                item.Operation.Cancel();
            }
            catch (Exception ex)
            {
                Log.Warn("[download] cancel failed: " + Log.Describe(ex));
            }

            Save();
            RaiseChanged();
            item.RaiseUpdated();
        }

        public void OpenFile(DownloadItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("[download] open file failed: " + Log.Describe(ex));
            }
        }

        public void OpenFolder(DownloadItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath))
            {
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(item.FilePath);

                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    return;
                }

                string arguments = File.Exists(item.FilePath)
                    ? "/select,\"" + item.FilePath + "\""
                    : "\"" + folder + "\"";

                Process.Start("explorer.exe", arguments);
            }
            catch (Exception ex)
            {
                Log.Warn("[download] open folder failed: " + Log.Describe(ex));
            }
        }

        /// <summary>
        /// 清空本次会话已完成/失败/取消的记录；进行中的保留。
        /// </summary>
        public void ClearFinished()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].IsSession && !_items[i].IsActive)
                {
                    _items.RemoveAt(i);
                }
            }

            Save();
            RaiseChanged();
        }

        /// <summary>
        /// 删除磁盘文件但保留下载记录；成功后清空 FilePath。
        /// </summary>
        public bool DeleteFile(DownloadItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
            {
                return false;
            }

            try
            {
                File.Delete(item.FilePath);
                item.FilePath = null;
                Save();
                RaiseChanged();
                item.RaiseUpdated();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("[download] delete file failed: " + Log.Describe(ex));
                return false;
            }
        }

        /// <summary>
        /// 删除一条记录（不删除磁盘文件）；进行中的先取消再删除。
        /// </summary>
        public void Delete(DownloadItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.IsActive)
            {
                Cancel(item);
            }

            _items.Remove(item);
            Save();
            RaiseChanged();
        }

        /// <summary>
        /// 退出前把进行中的记录标记为“上次退出时未完成”，避免下次显示为下载中。
        /// </summary>
        public void MarkActiveInterrupted()
        {
            bool changed = false;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsActive)
                {
                    _items[i].State = DownloadItem.StateInterrupted;
                    _items[i].Error = "上次退出时未完成";
                    _items[i].Paused = false;
                    _items[i].Operation = null;
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
                RaiseChanged();
            }
        }

        private void UpdateProgress(DownloadItem item)
        {
            CoreWebView2DownloadOperation operation = item.Operation;

            if (operation == null)
            {
                return;
            }

            try
            {
                item.BytesReceived = operation.BytesReceived;
                item.TotalBytes = ToLong(operation.TotalBytesToReceive);

                if (!string.IsNullOrEmpty(operation.ResultFilePath))
                {
                    item.FilePath = operation.ResultFilePath;
                    item.FileName = Path.GetFileName(operation.ResultFilePath);
                }
            }
            catch
            {
            }

            item.RaiseUpdated();
        }

        private void UpdateState(DownloadItem item)
        {
            CoreWebView2DownloadOperation operation = item.Operation;

            if (operation == null || string.Equals(item.State, DownloadItem.StateCancelled, StringComparison.Ordinal))
            {
                return;
            }

            // 暂停会触发 Interrupted(UserCanceled)；暂停期间忽略原始状态。
            if (item.Paused)
            {
                return;
            }

            try
            {
                if (operation.State == CoreWebView2DownloadState.Completed)
                {
                    item.State = DownloadItem.StateCompleted;
                    item.Paused = false;
                    item.Error = null;
                    long totalBytes = ToLong(operation.TotalBytesToReceive);
                    item.BytesReceived = totalBytes > 0 ? totalBytes : operation.BytesReceived;

                    if (!string.IsNullOrEmpty(operation.ResultFilePath))
                    {
                        item.FilePath = operation.ResultFilePath;
                        item.FileName = Path.GetFileName(operation.ResultFilePath);
                    }
                }
                else if (operation.State == CoreWebView2DownloadState.Interrupted)
                {
                    item.State = DownloadItem.StateInterrupted;
                    item.Paused = false;
                    item.Error = operation.InterruptReason.ToString();
                }
                else if (!item.Paused)
                {
                    item.State = DownloadItem.StateProgress;
                }
            }
            catch (Exception ex)
            {
                item.State = DownloadItem.StateInterrupted;
                item.Error = Log.Describe(ex);
            }

            Save();
            RaiseChanged();
            item.RaiseUpdated();
        }

        private void Load()
        {
            DownloadHistory history = JsonFile.Load<DownloadHistory>(_historyPath, null);

            if (history == null || history.Items == null)
            {
                return;
            }

            for (int i = 0; i < history.Items.Count; i++)
            {
                DownloadItem item = history.Items[i];

                if (item == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(item.State))
                {
                    item.State = DownloadItem.StateCompleted;
                }

                // 上次退出时仍未完成的，统一标成中断，不显示“下载中”。
                if (item.IsActive)
                {
                    item.State = DownloadItem.StateInterrupted;
                    item.Error = "上次退出时未完成";
                    item.Paused = false;
                }

                _items.Add(item);
            }
        }

        private void Save()
        {
            while (_items.Count > MaxItems)
            {
                int removeAt = -1;

                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (!_items[i].IsActive)
                    {
                        removeAt = i;
                        break;
                    }
                }

                if (removeAt < 0)
                {
                    break;
                }

                _items.RemoveAt(removeAt);
            }

            DownloadHistory history = new DownloadHistory();
            history.Items = _items;

            if (!_persist)
            {
                return;
            }

            JsonFile.Save(_historyPath, history);
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static long ToLong(ulong? value)
        {
            if (!value.HasValue)
            {
                return 0;
            }

            return value.Value > long.MaxValue ? long.MaxValue : (long)value.Value;
        }

        private static string DefaultDirectory()
        {
            return KnownFolders.DownloadsPath();
        }

        private static string SuggestFileName(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string name = Path.GetFileName(uri.LocalPath);

                if (!string.IsNullOrEmpty(name))
                {
                    return Uri.UnescapeDataString(name);
                }
            }
            catch
            {
            }

            return "download";
        }
    }
}
