using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.Browser
{
    /// <summary>
    /// BrowserTab 的丢弃与销毁逻辑（partial）。
    /// 依赖：Log。改动注意：丢弃前必须保存 URL 和标题，恢复时由 EnsureLiveAsync 重建。
    /// </summary>
    public sealed partial class BrowserTab
    {
        private readonly object _suspendGate = new object();
        private Task<bool> _suspendTask;

        /// <summary>
        /// 跟随系统明暗主题：窗口底色 + 网页 prefers-color-scheme。
        /// </summary>
        public void ApplyTheme()
        {
            BackColor = ThemeManager.Current.WebViewBackground;

            if (_webView != null && !_webView.IsDisposed)
            {
                _webView.DefaultBackgroundColor = ThemeManager.Current.WebViewBackground;
            }

            if (_core != null && !_isSuspended && !_disposed)
            {
                _core.Profile.PreferredColorScheme = ThemeManager.IsDark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
            }
        }

        /// <summary>
        /// 挂起后台标签：保留 DOM/表单/滚动位置，只暂停脚本和渲染。
        /// 调用前控件必须不可见，否则 TrySuspendAsync 会抛 COMException。
        /// </summary>
        public Task<bool> SuspendAsync()
        {
            lock (_suspendGate)
            {
                if (_suspendTask != null && !_suspendTask.IsCompleted)
                {
                    return _suspendTask;
                }

                _suspendTask = SuspendCoreAsync();
                return _suspendTask;
            }
        }

        private async Task<bool> SuspendCoreAsync()
        {
            DropDisposedWebView();

            WebView2 view = _webView;
            CoreWebView2 core = _core;

            if (view == null || core == null || _isSuspended || _disposed)
            {
                return false;
            }

            try
            {
                int stateVersion = _stateVersion;

                // 先记录脏表单状态，挂起后不能再执行脚本查询。
                _hasUnsavedInput = await IsDirtyAsync();

                // 等待期间标签可能被丢弃、激活或被替换，必须确认仍是同一实例且状态未变。
                if (IsStale(view, core) || stateVersion != _stateVersion)
                {
                    Log.Debug("[tab " + _id + "] suspend skipped (stale)");
                    return false;
                }

                bool suspended = await core.TrySuspendAsync();

                if (IsStale(view, core) || stateVersion != _stateVersion)
                {
                    // 挂起过程中标签被激活：立刻恢复，避免活动标签停在挂起态。
                    if (suspended && !IsStale(view, core) && core.IsSuspended)
                    {
                        try
                        {
                            core.Resume();
                        }
                        catch
                        {
                        }
                    }

                    Log.Debug("[tab " + _id + "] suspend result ignored (stale)");
                    return false;
                }

                _isSuspended = suspended;

                if (suspended)
                {
                    Log.Info("[tab " + _id + "] suspend: ok");
                }
                else
                {
                    // 挂起被拒绝（音频/下载/长连接等）：退到低内存目标级别。
                    SetMemoryTarget(true);
                    Log.Info("[tab " + _id + "] suspend: refused; memory target low");
                }

                return suspended;
            }
            catch (Exception ex)
            {
                if (IsStale(view, core))
                {
                    Log.Debug("[tab " + _id + "] suspend skipped (stale)");
                    return false;
                }

                if (IsDisposedError(ex))
                {
                    if (!IsCoreAlive(core, view))
                    {
                        DropDeadWebView(view);
                        return false;
                    }

                    // 对象仍可用：当作挂起被拒绝，用低内存目标兜底。
                    bool wasThrottled = _isThrottled;
                    SetMemoryTarget(true);

                    if (wasThrottled)
                    {
                        Log.Debug("[tab " + _id + "] suspend still refused");
                    }
                    else
                    {
                        Log.Info("[tab " + _id + "] suspend failed but core alive; memory target low");
                    }

                    return false;
                }

                Log.Warn("[tab " + _id + "] suspend failed: " + ex.Message);
                return false;
            }
        }

        public void Resume()
        {
            DropDisposedWebView();

            WebView2 view = _webView;
            CoreWebView2 core = _core;

            if (view == null || core == null || _disposed)
            {
                return;
            }

            try
            {
                if (core.IsSuspended)
                {
                    core.Resume();
                }
            }
            catch (Exception ex)
            {
                if (!IsStale(view, core))
                {
                    Log.Warn("[tab " + _id + "] resume failed: " + ex.Message);
                }
            }

            _stateVersion++;
            _isSuspended = false;
            SetMemoryTarget(false);
        }

        /// <summary>
        /// 页面里是否有未提交的表单改动。
        /// </summary>
        public async Task<bool> IsDirtyAsync()
        {
            DropDisposedWebView();

            CoreWebView2 core = _core;

            if (core == null || _disposed)
            {
                return false;
            }

            try
            {
                string result = await core.ExecuteScriptAsync("window.__vantageDirty === true");
                return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 真正销毁 WebView2：先保存 URL、标题和滚动位置。
        /// </summary>
        public async Task<bool> DiscardAsync()
        {
            DropDisposedWebView();

            WebView2 view = _webView;
            CoreWebView2 core = _core;

            if (view == null || _disposed)
            {
                return false;
            }

            if (core != null)
            {
                try
                {
                    if (core.IsSuspended)
                    {
                        core.Resume();
                    }

                    _url = core.Source;
                    _title = core.DocumentTitle;
                    await CaptureScrollAsync(core);
                }
                catch (Exception ex)
                {
                    Log.Debug("[tab " + _id + "] discard capture skipped: " + ex.Message);
                }
            }

            if (IsStale(view, core))
            {
                Log.Debug("[tab " + _id + "] discard skipped (stale)");
                return false;
            }

            // 内置页面无法按 URL 重建，丢弃后回到空白页。
            if (_internalAddress != null)
            {
                _url = "about:blank";
                _internalAddress = null;
            }

            _pendingUrl = _url;
            _restorePending = true;
            _stateVersion++;
            _isSuspended = false;

            Controls.Remove(view);
            view.Dispose();
            _webView = null;
            _core = null;

            Log.Info("[tab " + _id + "] discarded: " + _url);

            EventHandler<string> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, DisplayTitle);
            }

            return true;
        }

        private async Task CaptureScrollAsync(CoreWebView2 core)
        {
            try
            {
                string result = await core.ExecuteScriptAsync("window.scrollX + ',' + window.scrollY");
                string text = result == null ? string.Empty : result.Trim('"');
                string[] parts = text.Split(',');
                int x;
                int y;

                if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                {
                    _pendingScrollX = x;
                    _pendingScrollY = y;
                    _hasPendingScroll = true;
                }
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            if (disposing && _webView != null)
            {
                Controls.Remove(_webView);
                _webView.Dispose();
                _webView = null;
                _core = null;
            }

            base.Dispose(disposing);
        }

        private void DropDisposedWebView()
        {
            if (_webView != null && _webView.IsDisposed)
            {
                _webView = null;
                _core = null;
                _isSuspended = false;
            }
        }

        /// <summary>
        /// 控件还在、但 CoreWebView2 已经不可用（典型：controller 已释放）。
        /// 这种 WebView2 无法恢复，直接按丢弃处理，切回时重建。
        /// </summary>
        private void DropDeadWebView(WebView2 view)
        {
            if (!ReferenceEquals(_webView, view))
            {
                Log.Debug("[tab " + _id + "] suspend skipped (disposed)");
                return;
            }

            _webView = null;
            _core = null;
            _isSuspended = false;
            _isThrottled = false;
            _pendingUrl = _url;
            _restorePending = true;

            try
            {
                Controls.Remove(view);
                view.Dispose();
            }
            catch
            {
            }

            Log.Info("[tab " + _id + "] webview dropped (dead), restore on demand: " + _url);
        }

        /// <summary>
        /// 探活：真正触碰一次 COM 属性，判断 CoreWebView2 是否还能用。
        /// </summary>
        private static bool IsCoreAlive(CoreWebView2 core, WebView2 view)
        {
            if (core == null || view == null || view.IsDisposed)
            {
                return false;
            }

            try
            {
                string probe = core.Source;
                return probe != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDisposedError(Exception ex)
        {
            return ex != null
                && ex.Message != null
                && ex.Message.IndexOf("disposed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 设置 Chromium 内存目标级别。Low 是尽力而为，恢复活跃时必须显式设回 Normal。
        /// </summary>
        private void SetMemoryTarget(bool low)
        {
            CoreWebView2 core = _core;
            WebView2 view = _webView;

            if (core == null || view == null || view.IsDisposed || _disposed)
            {
                return;
            }

            if (low == _isThrottled)
            {
                return;
            }

            try
            {
                core.MemoryUsageTargetLevel = low
                    ? CoreWebView2MemoryUsageTargetLevel.Low
                    : CoreWebView2MemoryUsageTargetLevel.Normal;

                _isThrottled = low;
                Log.Info("[tab " + _id + "] memory target: " + (low ? "low" : "normal"));
            }
            catch (Exception ex)
            {
                Log.Debug("[tab " + _id + "] memory target failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 判断手里这对 view/core 是否已经失效（标签销毁、WebView2 被替换或控件已释放）。
        /// </summary>
        private bool IsStale(WebView2 view, CoreWebView2 core)
        {
            return _disposed
                || view == null
                || view.IsDisposed
                || !ReferenceEquals(_webView, view)
                || !ReferenceEquals(_core, core);
        }

    }
}
