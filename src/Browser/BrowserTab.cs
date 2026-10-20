using System;
using System.Drawing;
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
        private bool _disposed;
        private int _stateVersion;
        private bool _isThrottled;
        private bool _statusBarSuppressed;
        private bool _isLoading;
        private string _internalAddress;
        private string _pendingInternalAddress;
        private string _pendingInternalHtml;
        private Image _favicon;
        private Image _overrideFavicon;

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

        public new bool IsDisposed
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

        public bool IsLoading
        {
            get { return _isLoading; }
        }

        public bool IsThrottled
        {
            get { return _isThrottled; }
        }

        public Image Favicon
        {
            get { return _overrideFavicon ?? _favicon; }
        }

        /// <summary>
        /// 内置页使用 Vantage 自己的图标；传 null 清除。
        /// </summary>
        public void SetFaviconOverride(Image image)
        {
            if (ReferenceEquals(_overrideFavicon, image))
            {
                return;
            }

            if (_overrideFavicon != null)
            {
                _overrideFavicon.Dispose();
            }

            _overrideFavicon = image;

            EventHandler handler = FaviconChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
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
                if (_internalAddress != null)
                {
                    return _internalAddress;
                }

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
        public event EventHandler<NavigatedEventArgs> Navigated;
        public event EventHandler<string> WebMessage;
        public event EventHandler FaviconChanged;

        public BrowserTab(
            int id,
            CoreWebView2Environment environment,
            bool enableDevTools,
            string initialUrl)
            : this(id, environment, enableDevTools, initialUrl, null, null)
        {
        }

        public BrowserTab(
            int id,
            CoreWebView2Environment environment,
            bool enableDevTools,
            string initialUrl,
            string internalAddress,
            string internalHtml)
        {
            _id = id;
            _environment = environment;
            _enableDevTools = enableDevTools;
            _pendingUrl = internalAddress == null ? initialUrl : null;
            _url = internalAddress ?? initialUrl;
            _internalAddress = internalAddress;
            _pendingInternalAddress = internalAddress;
            _pendingInternalHtml = internalHtml;
            LastActivatedUtc = DateTime.UtcNow;
            LastDeactivatedUtc = DateTime.MinValue;

            Dock = DockStyle.Fill;
            BackColor = ThemeManager.Current.WebViewBackground;
        }

        /// <summary>
        /// 按地址重新生成内置页 HTML（丢弃/自愈后恢复用）。
        /// </summary>
        public Func<string, string> InternalHtmlProvider { get; set; }

        /// <summary>
        /// 显示内置页面（历史/书签）。地址栏保留 about: 地址，不计入浏览历史。
        /// </summary>
        public void ShowInternalPage(string address, string html)
        {
            _internalAddress = address;
            SetFaviconOverride(AppIcon.Create16());
            _url = address;

            EventHandler<string> handler = AddressChanged;
            if (handler != null)
            {
                handler(this, _url);
            }

            if (_core == null)
            {
                Log.Debug("[tab " + _id + "] internal page skipped (not live): " + address);
                return;
            }

            try
            {
                _core.NavigateToString(html ?? string.Empty);
            }
            catch (Exception ex)
            {
                Log.Warn("[tab " + _id + "] internal page failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 在内置页里执行脚本（例如主题切换时更新 CSS 变量）。挂起/未就绪时安全跳过。
        /// </summary>
        public void ExecuteScript(string script)
        {
            CoreWebView2 core = _core;

            if (core == null || _disposed || _isSuspended)
            {
                return;
            }

            try
            {
                core.ExecuteScriptAsync(script ?? string.Empty);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 当前 WebView2 的 Profile（Cookie / 浏览数据）。未就绪时返回 null。
        /// </summary>
        public CoreWebView2Profile Profile
        {
            get
            {
                CoreWebView2 core = _core;

                if (core == null || _disposed)
                {
                    return null;
                }

                try
                {
                    return core.Profile;
                }
                catch
                {
                    return null;
                }
            }
        }

        public void PostWebMessage(string text)
        {
            CoreWebView2 core = _core;

            if (core == null || _disposed)
            {
                return;
            }

            try
            {
                core.PostWebMessageAsString(text ?? string.Empty);
            }
            catch
            {
            }
        }

        public void Navigate(string url)
        {
            _internalAddress = null;

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

        /// <summary>
        /// 同步标签与内部 WebView2 的可见性。
        /// TrySuspendAsync 只认 CoreWebView2Controller.IsVisible，父容器隐藏不会自动同步。
        /// </summary>
        public void SetContentVisibility(bool visible)
        {
            WebView2 view = _webView;

            // 顺序很重要：WinForms 的 Visible getter 在父容器隐藏后恒为 false，
            // 那时再给子控件赋 false 是空操作，不会触发 VisibleChanged，
            // WebView2 controller 就会一直以为自己可见。
            if (visible)
            {
                Visible = true;

                if (view != null && !view.IsDisposed && !view.Visible)
                {
                    try { view.Visible = true; } catch { }
                }
            }
            else
            {
                if (view != null && !view.IsDisposed && view.Visible)
                {
                    try { view.Visible = false; } catch { }
                }

                Visible = false;
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
