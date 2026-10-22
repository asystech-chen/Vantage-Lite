using System;
using System.Collections.Generic;
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
        private TitleBar _titleBar;
        private Panel _contentHost;
        private TabManager _tabManager;
        private BrowserTab _attachedTab;
        private BookmarkStore _bookmarks;
        private readonly HashSet<string> _expandedBookmarks = new HashSet<string>(StringComparer.Ordinal);
        private HistoryStore _history;
        private DownloadManager _downloads;
        private DownloadManager _normalDownloads;
        private DownloadManager _privateDownloads;
        private DownloadsForm _downloadsForm;
        private DateTime _downloadsClosedUtc;
        private AppSettings _settings;
        private CoreWebView2Environment _environment;
        private TabManager _normalTabManager;
        private TabManager _privateTabManager;
        private bool _privacyMode;
        private bool _privacyTransition;
        private int _maxLiveTabs;
        private int _suspendAfterSeconds;
        private int _discardAfterMinutes;
        private Panel _privacyBanner;
        private Label _privacyLabel;
        private Button _privacyExitButton;
        private bool _minimized;
        private Timer _resizeSettleTimer;
        private Timer _faviconRefreshTimer;
        private Timer _privacyReclaimTimer;
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
            _normalDownloads = _downloads;
            WireDownloadManager(_downloads);
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

            if (_settings.ClearOnExit)
            {
                _settings.RestoreSession = false;
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
            RestoreWindowState();
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

            _privacyReclaimTimer = new Timer();
            _privacyReclaimTimer.Interval = 5 * 60 * 1000;
            _privacyReclaimTimer.Tick += PrivacyReclaimTimer_Tick;

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

        private string SessionPath
        {
            get { return Path.Combine(AppPaths.DataDirectory, "session.json"); }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                Log.Info("Creating WebView2 environment...");
                CoreWebView2Environment environment = await WebViewEnvironment.CreateAsync(_options);
                _environment = environment;

                _maxLiveTabs = _options.MaxLiveTabsSpecified
                    ? _options.MaxLiveTabs
                    : _settings.MaxLiveTabs;
                _suspendAfterSeconds = _options.SuspendSpecified
                    ? _options.SuspendAfterSeconds
                    : _settings.SuspendAfterSeconds;
                _discardAfterMinutes = _options.DiscardSpecified
                    ? _options.DiscardAfterMinutes
                    : _settings.DiscardAfterMinutes;

                _normalTabManager = new TabManager(
                    _contentHost,
                    environment,
                    _options.EnableDevTools,
                    _maxLiveTabs,
                    _suspendAfterSeconds,
                    _discardAfterMinutes,
                    false);
                _tabManager = _normalTabManager;
                WireTabManager(_normalTabManager);
                _normalTabManager.InternalHtmlProvider = BuildInternalPageHtml;
                _normalTabManager.SetDownloadDirectory(_settings.DownloadDirectory);

                _tabStrip.TabSelected += TabStrip_TabSelected;
                _tabStrip.TabCloseRequested += TabStrip_TabCloseRequested;
                _tabStrip.NewTabRequested += TabStrip_NewTabRequested;
                _tabStrip.ContextMenuRequested += TabStrip_ContextMenuRequested;
                _tabStrip.TabReorderRequested += TabStrip_TabReorderRequested;

                Log.Info("WebView2 environment ready. max live tabs = " + _maxLiveTabs);

                SessionData session = null;

                if (_settings.RestoreSession && !_settings.ClearOnExit)
                {
                    session = SessionStore.Load(SessionPath);
                }

                if (session != null && session.Tabs != null && session.Tabs.Count > 0)
                {
                    await RestoreSessionAsync(session);
                }
                else
                {
                    await OpenInitialTabAsync();
                }
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

        /// <summary>
        /// 恢复上次会话：先全部以丢弃态占位，只拉起活动标签的 WebView2。
        /// </summary>
        private async Task RestoreSessionAsync(SessionData session)
        {
            List<BrowserTab> restored = new List<BrowserTab>();

            for (int i = 0; i < session.Tabs.Count; i++)
            {
                SessionTab saved = session.Tabs[i];

                if (saved == null || string.IsNullOrWhiteSpace(saved.Url))
                {
                    continue;
                }

                string url = saved.Url.Trim();

                if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string internalAddress = url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                    ? url
                    : null;

                restored.Add(_tabManager.AddPlaceholderTab(url, internalAddress, saved.Title));
            }

            if (restored.Count == 0)
            {
                await OpenInitialTabAsync();
                return;
            }

            int activeIndex = Math.Max(0, Math.Min(session.ActiveIndex, restored.Count - 1));
            Log.Info("Restoring session: " + restored.Count + " tabs, active=" + activeIndex);

            try
            {
                await _tabManager.ActivateTabAsync(restored[activeIndex].Id);
                // 恢复的标签不是走 CreateTabAsync 创建的，需要补一次下载目录设置。
                _tabManager.SetDownloadDirectory(_settings.DownloadDirectory);
            }
            catch (Exception ex)
            {
                Log.Warn("Restore active tab failed: " + Log.Describe(ex));
            }

            if (_tabManager.ActiveTab == null || !_tabManager.ActiveTab.IsLive)
            {
                await OpenInitialTabAsync();
            }
        }

        /// <summary>
        /// 保存当前会话；开启关闭时清空或未启用会话恢复时删除会话文件。
        /// </summary>
        private void SaveSession()
        {
            string path = SessionPath;

            if (_settings == null || _normalTabManager == null ||
                !_settings.RestoreSession || _settings.ClearOnExit)
            {
                SessionStore.Clear(path);
                return;
            }

            List<SessionTab> tabs = new List<SessionTab>();
            int activeIndex = 0;

            for (int i = 0; i < _normalTabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _normalTabManager.Tabs[i];
                string url = tab.Url;

                if (string.IsNullOrWhiteSpace(url) ||
                    string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ReferenceEquals(tab, _normalTabManager.ActiveTab))
                {
                    activeIndex = tabs.Count;
                }

                tabs.Add(new SessionTab
                {
                    Url = url,
                    Title = tab.DisplayTitle
                });
            }

            if (tabs.Count == 0)
            {
                SessionStore.Clear(path);
                return;
            }

            SessionStore.Save(path, new SessionData
            {
                Tabs = tabs,
                ActiveIndex = Math.Min(activeIndex, tabs.Count - 1)
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveWindowState();

            if (_downloads != null)
            {
                _downloads.MarkActiveInterrupted();
            }

            if (_normalDownloads != null && !ReferenceEquals(_normalDownloads, _downloads))
            {
                _normalDownloads.MarkActiveInterrupted();
            }

            SaveSession();

            if (_settings == null || !_settings.ClearOnExit)
            {
                return;
            }

            try
            {
                Microsoft.Web.WebView2.Core.CoreWebView2Profile profile =
                    _normalTabManager == null ? null : _normalTabManager.GetProfile();

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
            // 隐私模式下普通工作区后台完成的重试下载不弹面板，只记录到普通下载列表。
            if (_privacyMode && ReferenceEquals(sender, _normalDownloads))
            {
                return;
            }

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

            if (_privacyReclaimTimer != null)
            {
                _privacyReclaimTimer.Stop();
                _privacyReclaimTimer.Dispose();
            }
            _history.Save();
            _bookmarks.Save();

            if (_privateDownloads != null)
            {
                _privateDownloads.Dispose();
                _privateDownloads = null;
            }

            if (_normalDownloads != null)
            {
                _normalDownloads.Dispose();
                _normalDownloads = null;
            }
            else if (_downloads != null)
            {
                _downloads.Dispose();
            }

            ShutdownTheme();

            if (_privateTabManager != null)
            {
                _privateTabManager.Dispose();
                _privateTabManager = null;
            }

            if (_normalTabManager != null)
            {
                _normalTabManager.Dispose();
                _normalTabManager = null;
            }

            _tabManager = null;
        }
    }
}
