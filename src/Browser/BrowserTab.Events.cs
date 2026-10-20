using System;
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
            _url = e.Uri;
            Log.Info("[tab " + _id + "] navigation starting: " + e.Uri);
            RaiseLoading(true);
        }

        private void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            RaiseLoading(false);

            if (e.IsSuccess)
            {
                RestorePendingScroll();
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
