using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
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
        /// <summary>
        /// 跟随系统明暗主题：窗口底色 + 网页 prefers-color-scheme。
        /// </summary>
        public void ApplyTheme()
        {
            BackColor = ThemeManager.Current.WebViewBackground;

            if (_webView != null)
            {
                _webView.DefaultBackgroundColor = ThemeManager.Current.WebViewBackground;
            }

            if (_core != null)
            {
                _core.Profile.PreferredColorScheme = ThemeManager.IsDark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
            }
        }

        /// <summary>
        /// 销毁 WebView2 释放内存，但保留标签本身和 URL/标题。
        /// </summary>
        public void Discard()
        {
            if (_webView == null)
            {
                return;
            }

            if (_core != null)
            {
                _url = _core.Source;
                _title = _core.DocumentTitle;
            }

            _pendingUrl = _url;
            _restorePending = true;

            Controls.Remove(_webView);
            _webView.Dispose();
            _webView = null;
            _core = null;

            Log.Info("[tab " + _id + "] discarded: " + _url);

            EventHandler<string> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, DisplayTitle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _webView != null)
            {
                Controls.Remove(_webView);
                _webView.Dispose();
                _webView = null;
                _core = null;
            }

            base.Dispose(disposing);
        }

    }
}
