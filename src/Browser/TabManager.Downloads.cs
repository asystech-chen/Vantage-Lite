using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的下载目录同步（partial）：把设置里的保存目录写到 WebView2 Profile。
    /// 依赖：CoreWebView2Profile / Log。改动注意：同一 user data 目录下的 Profile 共享，
    /// 逐标签设置是幂等的；空目录表示使用 WebView2 默认（系统下载文件夹）。
    /// </summary>
    public sealed partial class TabManager
    {
        private string _downloadDirectory;
        private bool _downloadDirectoryApplied;
        private BrowserTab _downloadWorker;

        /// <summary>
        /// 用一个隐藏标签在后台重新发起下载，避免重试时弹出可见标签。
        /// </summary>
        public async Task<bool> NavigateDownloadWorkerAsync(string url)
        {
            if (_disposed || string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            BrowserTab worker = _downloadWorker;

            try
            {
                if (worker == null || worker.IsDisposed || !worker.IsLive)
                {
                    worker = new BrowserTab(_nextId++, _environment, false, url, null, null, _privateMode);
                    worker.DownloadStarting += TabDownloadStarting;
                    _host.Controls.Add(worker);
                    worker.Dock = DockStyle.Fill;
                    worker.SendToBack();
                    // 初始化期间保持可见，WebView2 才能在隐藏前建立 CoreWebView2。
                    worker.SetContentVisibility(true);
                    await worker.EnsureLiveAsync();
                    worker.SetContentVisibility(false);
                    _downloadWorker = worker;
                    Log.Info("[download] worker created");
                    return true;
                }

                worker.Navigate(url);
                Log.Info("[download] worker navigate: " + url);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("[download] worker failed: " + Log.Describe(ex));

                try
                {
                    if (worker != null)
                    {
                        worker.Dispose();
                    }
                }
                catch
                {
                }

                _downloadWorker = null;
                return false;
            }
        }

        /// <summary>
        /// 设置下载目录；null/空字符串表示恢复 WebView2 默认目录。
        /// </summary>
        public void SetDownloadDirectory(string path)
        {
            _downloadDirectory = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
            _downloadDirectoryApplied = false;

            if (_downloadDirectory != null)
            {
                try
                {
                    Directory.CreateDirectory(_downloadDirectory);
                }
                catch (Exception ex)
                {
                    Log.Warn("创建下载目录失败：" + Log.Describe(ex));
                    return;
                }
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                ApplyDownloadDirectory(_tabs[i]);
            }
        }

        /// <summary>
        /// 标签 WebView2 就绪后调用；Profile 未就绪时静默跳过。
        /// </summary>
        internal void ApplyDownloadDirectory(BrowserTab tab)
        {
            if (tab == null || _downloadDirectoryApplied)
            {
                return;
            }

            CoreWebView2Profile profile = tab.Profile;
            if (profile == null)
            {
                return;
            }

            try
            {
                // 空设置表示系统默认：显式写 Downloads，避免部分运行时对 null 抛异常。
                profile.DefaultDownloadFolderPath = _downloadDirectory ?? DefaultDownloadDirectory();
                _downloadDirectoryApplied = true;
            }
            catch (Exception ex)
            {
                Log.Warn("设置下载目录失败：" + Log.Describe(ex));
            }
        }

        private static string DefaultDownloadDirectory()
        {
            return KnownFolders.DownloadsPath();
        }
    }
}
