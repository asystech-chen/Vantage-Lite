using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VantageLite.Theming;

namespace VantageLite.Browser
{
    /// <summary>
    /// 单个标签：持有一个 WebView2，负责自身的导航、标题、加载状态。
    /// 依赖：WebView2 / Log。改动注意：丢弃标签时必须保存 URL 和标题，恢复时重建 WebView2。
    /// </summary>
    public sealed partial class BrowserTab : UserControl
    {
        private readonly int _id;
        private readonly CoreWebView2Environment _environment;
        private readonly bool _enableDevTools;

        private WebView2 _webView;
        private CoreWebView2 _core;
        private string _pendingUrl;
        private string _url;
        private string _title;
        private bool _restorePending;
        private bool _isSuspended;
        private bool _hasPendingScroll;
        private int _pendingScrollX;
        private int _pendingScrollY;
        private bool _hasUnsavedInput;
        private bool _disposed;
        private int _stateVersion;
        private bool _isThrottled;

        public int Id
        {
            get { return _id; }
        }

        public bool IsLive
        {
            get { return !_disposed && _webView != null && !_webView.IsDisposed; }
        }

        public bool IsDiscarded
        {
            get { return !IsLive; }
        }

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        public bool IsSuspended
        {
            get { return _isSuspended; }
        }

        public bool HasUnsavedInput
        {
            get { return _hasUnsavedInput; }
        }

        public bool IsThrottled
        {
            get { return _isThrottled; }
        }

        /// <summary>
        /// 页面是否正在播放音频/视频。播放中的标签不能被挂起或丢弃，否则会中断播放。
        /// </summary>
        public bool IsPlayingAudio
        {
            get
            {
                CoreWebView2 core = _core;
                WebView2 view = _webView;

                if (core == null || view == null || view.IsDisposed || _disposed)
                {
                    return false;
                }

                try
                {
                    return core.IsDocumentPlayingAudio;
                }
                catch
                {
                    return false;
                }
            }
        }

        public DateTime LastDeactivatedUtc { get; set; }

        public string Url
        {
            get
            {
                WebView2 view = _webView;
                CoreWebView2 core = _core;

                if (core != null && view != null && !view.IsDisposed)
                {
                    try
                    {
                        return core.Source;
                    }
                    catch
                    {
                        // 控件释放过程中读取会抛异常，退回缓存的 URL。
                    }
                }

                return _url;
            }
        }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_title))
                {
                    return _title;
                }

                if (string.IsNullOrEmpty(_url) || _url == "about:blank")
                {
                    return "新标签页";
                }

                return _url;
            }
        }

        public DateTime LastActivatedUtc { get; set; }

        public event EventHandler<string> TitleChanged;
        public event EventHandler<string> AddressChanged;
        public event EventHandler NavigationStateChanged;
        public event EventHandler<bool> LoadingChanged;
        public event EventHandler<string> NewWindowRequested;
        public event EventHandler<string> ProcessFailed;

        public BrowserTab(
            int id,
            CoreWebView2Environment environment,
            bool enableDevTools,
            string initialUrl)
        {
            _id = id;
            _environment = environment;
            _enableDevTools = enableDevTools;
            _pendingUrl = initialUrl;
            _url = initialUrl;
            LastActivatedUtc = DateTime.UtcNow;
            LastDeactivatedUtc = DateTime.MinValue;

            Dock = DockStyle.Fill;
            BackColor = ThemeManager.Current.WebViewBackground;
        }

        public void Navigate(string url)
        {
            if (_core == null)
            {
                _pendingUrl = url;
                _url = url;
                return;
            }

            _core.Navigate(url);
        }

        public bool CanGoBack
        {
            get { return _core != null && _core.CanGoBack; }
        }

        public bool CanGoForward
        {
            get { return _core != null && _core.CanGoForward; }
        }

        public void GoBack()
        {
            if (_core != null && _core.CanGoBack)
            {
                _core.GoBack();
            }
        }

        public void GoForward()
        {
            if (_core != null && _core.CanGoForward)
            {
                _core.GoForward();
            }
        }

        public void Reload()
        {
            if (_core != null)
            {
                _core.Reload();
            }
        }

        public void Stop()
        {
            if (_core != null)
            {
                _core.Stop();
            }
        }

        public void FocusContent()
        {
            if (_webView != null)
            {
                _webView.Focus();
            }
        }

    }
}
