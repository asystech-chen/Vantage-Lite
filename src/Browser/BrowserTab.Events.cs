using System;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// BrowserTab 的 WebView2 事件处理（partial）。
    /// 依赖：Log。改动注意：这里只同步 URL/标题/加载状态，并向上转发事件。
    /// </summary>
    public sealed partial class BrowserTab
    {
        private void ConfigureCore()
        {
            _core.Settings.AreDevToolsEnabled = _enableDevTools;
            _core.Settings.AreDefaultContextMenusEnabled = true;
            _core.Settings.IsStatusBarEnabled = true;
            _core.Settings.IsSwipeNavigationEnabled = false;

            _core.SourceChanged += Core_SourceChanged;
            _core.DocumentTitleChanged += Core_DocumentTitleChanged;
            _core.HistoryChanged += Core_HistoryChanged;
            _core.NavigationStarting += Core_NavigationStarting;
            _core.NavigationCompleted += Core_NavigationCompleted;
            _core.NewWindowRequested += Core_NewWindowRequested;
            _core.ProcessFailed += Core_ProcessFailed;
        }

        private void Core_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            _url = _core.Source;
            Log.Debug("[tab " + _id + "] source: " + _url);

            EventHandler<string> handler = AddressChanged;
            if (handler != null)
            {
                handler(this, _url);
            }
        }

        private void Core_DocumentTitleChanged(object sender, object e)
        {
            _title = _core.DocumentTitle;

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
    }
}
