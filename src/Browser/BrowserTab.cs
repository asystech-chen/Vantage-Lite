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

        public int Id
        {
            get { return _id; }
        }

        public bool IsLive
        {
            get { return _webView != null; }
        }

        public bool IsDiscarded
        {
            get { return _webView == null; }
        }

        public bool IsSuspended
        {
            get { return _isSuspended; }
        }

        public bool HasUnsavedInput
        {
            get { return _hasUnsavedInput; }
        }

        public DateTime LastDeactivatedUtc { get; set; }

        public string Url
        {
            get { return _core != null ? _core.Source : _url; }
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

        public async Task EnsureLiveAsync()
        {
            if (_webView != null)
            {
                return;
            }

            Log.Info("[tab " + _id + "] creating WebView2");
            WebView2 view = new WebView2();
            view.Dock = DockStyle.Fill;
            view.DefaultBackgroundColor = ThemeManager.Current.WebViewBackground;

            try
            {
                Controls.Add(view);
                await view.EnsureCoreWebView2Async(_environment);
                _webView = view;
                _core = view.CoreWebView2;
                ConfigureCore();
                _isSuspended = false;

                // 页面内表单改动标记，用于丢弃前的脏数据保护。
                await _core.AddScriptToExecuteOnDocumentCreatedAsync(DirtyFormScript.Source);
            }
            catch
            {
                Controls.Remove(view);
                view.Dispose();
                throw;
            }

            string url = _restorePending ? _url : _pendingUrl;
            _restorePending = false;
            _pendingUrl = null;

            if (!string.IsNullOrEmpty(url))
            {
                Navigate(url);
            }
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
