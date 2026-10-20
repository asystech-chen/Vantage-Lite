using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
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
        private HistoryStore _history;
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
                DialogResult result = MessageBox.Show(
                    this,
                    "未检测到 WebView2 Runtime。\n\nVantage Lite 需要 Microsoft Edge WebView2 Runtime 才能运行。\n是否打开官方下载页面？",
                    "Vantage Lite",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

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

                int maxLiveTabs = _options.MaxLiveTabs != AppSettings.DefaultMaxLiveTabs
                    ? _options.MaxLiveTabs
                    : _settings.MaxLiveTabs;

                _tabManager = new TabManager(
                    _contentHost,
                    environment,
                    _options.EnableDevTools,
                    maxLiveTabs,
                    _options.SuspendAfterSeconds,
                    _options.DiscardAfterMinutes);

                _tabManager.TabsChanged += TabManager_TabsChanged;
                _tabManager.ActiveTabChanged += TabManager_ActiveTabChanged;
                _tabManager.NewWindowRequested += TabManager_NewWindowRequested;
                _tabManager.Navigated += TabManager_Navigated;
                _tabManager.WebMessage += TabManager_WebMessage;

                _tabStrip.TabSelected += TabStrip_TabSelected;
                _tabStrip.TabCloseRequested += TabStrip_TabCloseRequested;
                _tabStrip.NewTabRequested += TabStrip_NewTabRequested;

                Log.Info("WebView2 environment ready. max live tabs = " + maxLiveTabs);
                await _tabManager.CreateTabAsync(HomeUrl);
            }
            catch (Exception ex)
            {
                Log.Error("WebView2 初始化失败", ex);
                MessageBox.Show(
                    this,
                    "浏览器内核初始化失败：\n" + ex.Message,
                    "Vantage Lite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Log.Info("Main window closed.");
            Application.RemoveMessageFilter(this);
            _resizeSettleTimer.Dispose();
            _faviconRefreshTimer.Dispose();
            _history.Save();
            _bookmarks.Save();
            ShutdownTheme();

            if (_tabManager != null)
            {
                _tabManager.Dispose();
                _tabManager = null;
            }
        }
    }
}
