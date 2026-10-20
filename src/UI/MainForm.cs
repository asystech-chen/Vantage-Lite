using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Downloads;
using VantageLite.Storage;

namespace VantageLite.UI
{
    /// <summary>
    /// 主窗口：创建 WebView2 环境、装配标签管理器、处理导航与快捷键。
    /// 依赖：Browser / Diagnostics / App。改动注意：布局在 MainForm.Layout.cs，事件在 MainForm.Events.cs。
    /// </summary>
    public sealed partial class MainForm : Form, IMessageFilter
    {
        private const string DefaultHomeUrl = "https://asystech.cn/start/index.html";

        private readonly CommandLineOptions _options;

        private NavigationBar _navigationBar;
        private BookmarksBar _bookmarksBar;
        private TabStrip _tabStrip;
        private Panel _contentHost;
        private TabManager _tabManager;
        private BrowserTab _attachedTab;
        private BookmarkStore _bookmarks;
        private readonly HashSet<string> _expandedBookmarks = new HashSet<string>(StringComparer.Ordinal);
        private HistoryStore _history;
        private DownloadManager _downloads;
        private DownloadsForm _downloadsForm;
        private DateTime _downloadsClosedUtc;
        private AppSettings _settings;
        private bool _minimized;
        private Timer _resizeSettleTimer;
        private Timer _faviconRefreshTimer;
        private string _faviconRefreshAddress;
        private int _faviconRefreshVersion;

        public MainForm()
            : this(new CommandLineOptions())
        {
        }

        public MainForm(CommandLineOptions options)
        {
            _options = options ?? new CommandLineOptions();
            _bookmarks = new BookmarkStore(Path.Combine(AppPaths.DataDirectory, "bookmarks.json"));
            _history = new HistoryStore(Path.Combine(AppPaths.DataDirectory, "history.json"));
            _downloads = new DownloadManager(Path.Combine(AppPaths.DataDirectory, "downloads.json"));
            _downloads.Started += Downloads_Started;
            _downloads.Changed += delegate
            {
                // 下载事件可能来自页面消息处理，延后重绘，避免在 WebMessage 回调里导航页面。
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(RenderDownloadsPage));
                    }
                    catch
                    {
                    }
                }
            };
            _settings = JsonFile.Load<AppSettings>(Path.Combine(AppPaths.DataDirectory, "settings.json"), null)
                ?? AppSettings.CreateDefault();

            if (string.IsNullOrWhiteSpace(_settings.HomeUrl))
            {
                _settings.HomeUrl = AppSettings.DefaultHomeUrl;
            }

            if (_settings.MaxLiveTabs < 1)
            {
                _settings.MaxLiveTabs = AppSettings.DefaultMaxLiveTabs;
            }

            if (_settings.SuspendAfterSeconds < 0 || _settings.SuspendAfterSeconds > 3600)
            {
                _settings.SuspendAfterSeconds = AppSettings.DefaultSuspendSeconds;
            }

            if (_settings.DiscardAfterMinutes < 0 || _settings.DiscardAfterMinutes > 1440)
            {
                _settings.DiscardAfterMinutes = AppSettings.DefaultDiscardMinutes;
            }

            if (string.IsNullOrWhiteSpace(_settings.SearchEngine))
            {
                _settings.SearchEngine = AppSettings.DefaultSearchEngine;
            }

            if (string.IsNullOrWhiteSpace(_settings.NewTabMode))
            {
                _settings.NewTabMode = AppSettings.DefaultNewTabMode;
            }

            if (_settings.NewTabUrl == null)
            {
                _settings.NewTabUrl = string.Empty;
            }

            if (_settings.DownloadDirectory == null)
            {
                _settings.DownloadDirectory = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(_settings.ThemeMode))
            {
                _settings.ThemeMode = AppSettings.DefaultThemeMode;
            }

            if (string.IsNullOrWhiteSpace(_settings.HomeMode))
            {
                _settings.HomeMode = string.Equals(
                    _settings.HomeUrl, AppSettings.DefaultHomeUrl, StringComparison.OrdinalIgnoreCase)
                    ? AppSettings.DefaultHomeMode
                    : "custom";
            }

            if (_settings.CustomSearchEngines == null)
            {
                _settings.CustomSearchEngines = new List<CustomSearchEngine>();
            }

            // 先确定明暗主题，再建控件，避免书签栏等控件在浅色配色下初始化。
            VantageLite.Theming.ThemeManager.Initialize();
            VantageLite.Theming.ThemeManager.SetMode(_settings.ThemeMode);

            BuildLayout();
            WireNavigationEvents();
            InitializeTheme();

            _bookmarksBar.Refresh(_bookmarks);
            _bookmarksBar.Visible = _settings.ShowBookmarksBar;

            _resizeSettleTimer = new Timer();
            _resizeSettleTimer.Interval = 250;
            _resizeSettleTimer.Tick += ResizeSettleTimer_Tick;

            _faviconRefreshTimer = new Timer();
            _faviconRefreshTimer.Interval = 1500;
            _faviconRefreshTimer.Tick += FaviconRefreshTimer_Tick;

            Application.AddMessageFilter(this);

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            Resize += MainForm_Resize;
        }

        private BrowserTab ActiveTab
        {
            get { return _tabManager == null ? null : _tabManager.ActiveTab; }
        }

        private string HomeUrl
        {
            get
            {
                return _settings != null && !string.IsNullOrWhiteSpace(_settings.HomeUrl)
                    ? _settings.HomeUrl
                    : AppSettings.DefaultHomeUrl;
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (!WebViewEnvironment.IsRuntimeAvailable)
            {
                DialogResult result = MessageDialog.Show(
                    this,
                    "未检测到 WebView2 Runtime。\n\nVantage Lite 需要 Microsoft Edge WebView2 Runtime 才能运行。\n是否打开官方下载页面？",
                    "Vantage Lite",
                    MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start("https://developer.microsoft.com/microsoft-edge/webview2/");
                    }
                    catch
                    {
                    }
                }

                Close();
                return;
            }

            try
            {
                Log.Info("Creating WebView2 environment...");
                CoreWebView2Environment environment = await WebViewEnvironment.CreateAsync(_options);

                int maxLiveTabs = _options.MaxLiveTabsSpecified
                    ? _options.MaxLiveTabs
                    : _settings.MaxLiveTabs;
                int suspendAfterSeconds = _options.SuspendSpecified
                    ? _options.SuspendAfterSeconds
                    : _settings.SuspendAfterSeconds;
                int discardAfterMinutes = _options.DiscardSpecified
                    ? _options.DiscardAfterMinutes
                    : _settings.DiscardAfterMinutes;

                _tabManager = new TabManager(
                    _contentHost,
                    environment,
                    _options.EnableDevTools,
                    maxLiveTabs,
                    suspendAfterSeconds,
                    discardAfterMinutes);

                _tabManager.TabsChanged += TabManager_TabsChanged;
                _tabManager.ActiveTabChanged += TabManager_ActiveTabChanged;
                _tabManager.NewWindowRequested += TabManager_NewWindowRequested;
                _tabManager.Navigated += TabManager_Navigated;
                _tabManager.WebMessage += TabManager_WebMessage;
                _tabManager.LastTabClosed += delegate { CreateNewTab(); };
                _tabManager.DownloadStarting += delegate(object downloadSender, CoreWebView2DownloadStartingEventArgs downloadArgs)
                {
                    _downloads.Begin(downloadArgs);
                };
                _tabManager.InternalHtmlProvider = BuildInternalPageHtml;

                _tabManager.SetDownloadDirectory(_settings.DownloadDirectory);

                _tabStrip.TabSelected += TabStrip_TabSelected;
                _tabStrip.TabCloseRequested += TabStrip_TabCloseRequested;
                _tabStrip.NewTabRequested += TabStrip_NewTabRequested;

                Log.Info("WebView2 environment ready. max live tabs = " + maxLiveTabs);
                await OpenInitialTabAsync();
            }
            catch (Exception ex)
            {
                Log.Error("WebView2 初始化失败", ex);
                MessageDialog.Show(
                    this,
                    "浏览器内核初始化失败：\n" + Log.Describe(ex),
                    "Vantage Lite",
                    MessageBoxButtons.OK);
                Close();
            }
        }

        /// <summary>
        /// 首次标签创建：WebView2 偶发初始化失败时重试一次，避免直接闪退。
        /// </summary>
        private async Task OpenInitialTabAsync()
        {
            string target = ResolveHomeTarget();

            try
            {
                await CreateTabForTargetAsync(target);
                return;
            }
            catch (Exception ex)
            {
                Log.Warn("首次标签创建失败，准备重试：" + Log.Describe(ex));
            }

            await Task.Delay(500);
            await CreateTabForTargetAsync(target);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_downloads != null)
            {
                _downloads.MarkActiveInterrupted();
            }

            if (_settings == null || !_settings.ClearOnExit)
            {
                return;
            }

            try
            {
                Microsoft.Web.WebView2.Core.CoreWebView2Profile profile =
                    _tabManager == null ? null : _tabManager.GetProfile();

                if (profile != null)
                {
                    profile.CookieManager.DeleteAllCookies();
                    Log.Info("Cookies cleared on exit");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Clear cookies on exit failed: " + ex.Message);
            }

            _history.Clear();
            Log.Info("History cleared on exit");
        }

        private void Downloads_Started(object sender, DownloadItem item)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            // 下载事件来自 WebView2 回调，延后到消息循环再开面板，避免重入。
            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(ShowDownloadsPanel));
                }
                catch
                {
                }

                return;
            }

            ShowDownloadsPanel();
        }

        private void ShowDownloadsPanel()
        {
            if (_downloadsForm != null && !_downloadsForm.IsDisposed)
            {
                _downloadsForm.RefreshItems();
                _downloadsForm.Activate();
                return;
            }

            _downloadsForm = new DownloadsForm(_downloads);
            _downloadsForm.FormClosed += delegate
            {
                _downloadsForm = null;
                _downloadsClosedUtc = DateTime.UtcNow;
            };
            _downloadsForm.ViewAllRequested += delegate
            {
                _downloadsForm.Close();
                OpenInternalPage(DownloadsAddress);
            };
            _downloadsForm.RetryRequested += delegate(object sender, DownloadItem item)
            {
                RetryDownload(item);
            };

            // 弹出在下载按钮下方，而不是屏幕角落。
            Point anchor = _navigationBar.GetDownloadsAnchor();
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int x = anchor.X - _downloadsForm.Width;
            int y = anchor.Y + 4;
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - _downloadsForm.Width - 8));
            y = Math.Max(area.Top + 8, Math.Min(y, area.Bottom - _downloadsForm.Height - 8));

            _downloadsForm.Location = new Point(x, y);
            _downloadsForm.Show(this);
        }

        /// <summary>
        /// 按钮/菜单/快捷键切换面板。面板因点到外面自动收起后，短时间内不再重开，
        /// 避免“点按钮关闭又被同一次点击打开”。
        /// </summary>
        private void ToggleDownloadsPanel()
        {
            if (_downloadsForm != null && !_downloadsForm.IsDisposed)
            {
                _downloadsForm.Close();
                return;
            }

            if ((DateTime.UtcNow - _downloadsClosedUtc).TotalMilliseconds < 250)
            {
                return;
            }

            ShowDownloadsPanel();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Log.Info("Main window closed.");
            Application.RemoveMessageFilter(this);
            _resizeSettleTimer.Dispose();
            _faviconRefreshTimer.Dispose();
            _history.Save();
            _bookmarks.Save();

            if (_downloads != null)
            {
                _downloads.Dispose();
            }

            ShutdownTheme();

            if (_tabManager != null)
            {
                _tabManager.Dispose();
                _tabManager = null;
            }
        }
    }
}
