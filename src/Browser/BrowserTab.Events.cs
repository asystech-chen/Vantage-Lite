using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.Browser
{
    /// <summary>
    /// BrowserTab 的 WebView2 事件处理（partial）。
    /// 依赖：Log。改动注意：这里只同步 URL/标题/加载状态，并向上转发事件。
    /// </summary>
    public sealed partial class BrowserTab
    {
        private void ConfigureCore(CoreWebView2 core)
        {
            core.Settings.AreDevToolsEnabled = _enableDevTools;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = true;
            core.Settings.IsSwipeNavigationEnabled = false;
            core.Profile.PreferredColorScheme = ThemeManager.IsDark
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;

            core.SourceChanged += Core_SourceChanged;
            core.DocumentTitleChanged += Core_DocumentTitleChanged;
            core.HistoryChanged += Core_HistoryChanged;
            core.NavigationStarting += Core_NavigationStarting;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.ProcessFailed += Core_ProcessFailed;
            core.WebMessageReceived += Core_WebMessageReceived;
            core.FaviconChanged += Core_FaviconChanged;
        }

        /// <summary>
        /// 暂时关闭内置链接预览条。窗口 resize 时预览条不会跟随移动，
        /// 会留在旧位置形成残影；先关掉，等缩放结束后再恢复。
        /// </summary>
        public void SuppressStatusBar()
        {
            CoreWebView2 core = _core;
            if (core == null || _statusBarSuppressed)
            {
                return;
            }

            _statusBarSuppressed = true;

            try
            {
                core.Settings.IsStatusBarEnabled = false;
            }
            catch
            {
                _statusBarSuppressed = false;
            }
        }

        public void RestoreStatusBar()
        {
            if (!_statusBarSuppressed)
            {
                return;
            }

            _statusBarSuppressed = false;

            CoreWebView2 core = _core;
            if (core == null)
            {
                return;
            }

            try
            {
                core.Settings.IsStatusBarEnabled = true;
            }
            catch
            {
            }
        }

        private void Core_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            CoreWebView2 core = sender as CoreWebView2;
            if (core == null || !ReferenceEquals(core, _core))
            {
                return;
            }

            // NavigateToString 会生成 data:text/html（或 about:blank），内置页面不覆盖地址栏。
            if (_internalAddress != null && IsInternalPageUri(core.Source))
            {
                return;
            }

            _internalAddress = null;
            _url = core.Source;
            Log.Debug("[tab " + _id + "] source: " + _url);

            EventHandler<string> handler = AddressChanged;
            if (handler != null)
            {
                handler(this, _url);
            }
        }

        private void Core_DocumentTitleChanged(object sender, object e)
        {
            CoreWebView2 core = sender as CoreWebView2;
            if (core == null || !ReferenceEquals(core, _core))
            {
                return;
            }

            _title = core.DocumentTitle;

            EventHandler<string> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, DisplayTitle);
            }
        }

        private void Core_HistoryChanged(object sender, object e)
        {
            EventHandler handler = NavigationStateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void Core_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            CoreWebView2 core = sender as CoreWebView2;
            if (core == null || !ReferenceEquals(core, _core))
            {
                return;
            }

            if (_internalAddress != null && IsInternalPageUri(e.Uri))
            {
                RaiseLoading(true);
                return;
            }

            ResetFavicon();
            _url = e.Uri;
            Log.Info("[tab " + _id + "] navigation starting: " + e.Uri);
            RaiseLoading(true);
        }

        private static bool IsInternalPageUri(string uri)
        {
            return string.IsNullOrEmpty(uri)
                || string.Equals(uri, "about:blank", StringComparison.OrdinalIgnoreCase)
                || uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RefreshFaviconAsync(CoreWebView2 core)
        {
            await Task.Delay(400);

            if (_disposed || !ReferenceEquals(core, _core))
            {
                return;
            }

            await LoadFaviconAsync(core);
        }

        private async void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            CoreWebView2 core = sender as CoreWebView2;
            if (core == null || !ReferenceEquals(core, _core))
            {
                return;
            }

            RaiseLoading(false);

            if (e.IsSuccess)
            {
                RestorePendingScroll();

                if (_internalAddress == null)
                {
                    EventHandler<NavigatedEventArgs> handler = Navigated;
                    if (handler != null)
                    {
                        handler(this, new NavigatedEventArgs(core.Source, DisplayTitle));
                    }
                }

                // FaviconChanged 不一定会触发，导航完成后主动取一次。
                await RefreshFaviconAsync(core);
                return;
            }

            // 旧导航被新请求取代时会产生这两个状态，不是真的网络故障。
            if (e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted ||
                e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
            {
                Log.Debug("[tab " + _id + "] navigation superseded: " + e.WebErrorStatus);
                return;
            }

            Log.Warn("[tab " + _id + "] navigation failed: " + e.WebErrorStatus + " url=" + _url);
        }

        private void Core_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            string uri = string.IsNullOrEmpty(e.Uri) ? "about:blank" : e.Uri;
            Log.Info("[tab " + _id + "] new window requested: " + uri);

            EventHandler<string> handler = NewWindowRequested;
            if (handler != null)
            {
                handler(this, uri);
            }
        }

        private async void Core_FaviconChanged(object sender, object e)
        {
            CoreWebView2 core = sender as CoreWebView2;
            if (core == null || !ReferenceEquals(core, _core))
            {
                return;
            }

            Log.Info("[tab " + _id + "] favicon: changed");
            await LoadFaviconAsync(core);
        }

        private async Task LoadFaviconAsync(CoreWebView2 core)
        {
            try
            {
                using (Stream stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                {
                    if (stream == null)
                    {
                        Log.Info("[tab " + _id + "] favicon: none");
                        return;
                    }

                    using (MemoryStream buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        buffer.Position = 0;

                        using (Image decoded = Image.FromStream(buffer))
                        {
                            Log.Info("[tab " + _id + "] favicon: loaded " + decoded.Width + "x" + decoded.Height);
                            SetFavicon(new Bitmap(decoded, 16, 16));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[tab " + _id + "] favicon failed: " + ex.Message);
            }
        }

        private void SetFavicon(Image image)
        {
            if (_disposed)
            {
                if (image != null)
                {
                    image.Dispose();
                }

                return;
            }

            if (_favicon != null)
            {
                _favicon.Dispose();
            }

            _favicon = image;

            EventHandler handler = FaviconChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ResetFavicon()
        {
            SetFaviconOverride(null);

            if (_favicon != null)
            {
                _favicon.Dispose();
                _favicon = null;

                EventHandler handler = FaviconChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private void Core_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!ReferenceEquals(sender, _core))
            {
                return;
            }

            string message;

            try
            {
                message = e.TryGetWebMessageAsString();
            }
            catch
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            EventHandler<string> handler = WebMessage;
            if (handler != null)
            {
                handler(this, message);
            }
        }

        private void Core_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            Log.Error("[tab " + _id + "] process failed: " + e.ProcessFailedKind);

            EventHandler<string> handler = ProcessFailed;
            if (handler != null)
            {
                handler(this, e.ProcessFailedKind.ToString());
            }
        }

        private void RaiseLoading(bool loading)
        {
            _isLoading = loading;

            EventHandler<bool> handler = LoadingChanged;
            if (handler != null)
            {
                handler(this, loading);
            }
        }

        private void RestorePendingScroll()
        {
            if (!_hasPendingScroll || _core == null)
            {
                return;
            }

            int x = _pendingScrollX;
            int y = _pendingScrollY;
            _hasPendingScroll = false;

            try
            {
                _ = _core.ExecuteScriptAsync("window.scrollTo(" + x + "," + y + ")");
            }
            catch
            {
            }
        }
    }
}
