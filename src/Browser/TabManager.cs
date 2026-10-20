using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// 标签生命周期管理：创建、切换、关闭、LRU 丢弃。
    /// 依赖：BrowserTab / Log。改动注意：活动标签永远不挂起、不销毁。
    /// </summary>
    public sealed partial class TabManager : IDisposable
    {
        private readonly Control _host;
        private readonly CoreWebView2Environment _environment;
        private readonly bool _enableDevTools;
        private readonly int _maxLiveTabs;
        private readonly int _suspendAfterSeconds;
        private readonly int _discardAfterMinutes;
        private readonly List<BrowserTab> _tabs;
        private readonly Timer _maintenanceTimer;

        private int _nextId;
        private int _activationVersion;
        private bool _windowMinimized;
        private bool _disposed;
        private bool _maintenanceBusy;

        public event EventHandler TabsChanged;
        public event EventHandler ActiveTabChanged;
        public event EventHandler Empty;
        public event EventHandler<string> NewWindowRequested;

        public TabManager(
            Control host,
            CoreWebView2Environment environment,
            bool enableDevTools,
            int maxLiveTabs,
            int suspendAfterSeconds,
            int discardAfterMinutes)
        {
            _host = host;
            _environment = environment;
            _enableDevTools = enableDevTools;
            _maxLiveTabs = Math.Max(1, maxLiveTabs);
            _suspendAfterSeconds = Math.Max(0, suspendAfterSeconds);
            _discardAfterMinutes = Math.Max(0, discardAfterMinutes);
            _tabs = new List<BrowserTab>();
            _nextId = 1;

            _maintenanceTimer = new Timer();
            _maintenanceTimer.Interval = 5000;
            _maintenanceTimer.Tick += MaintenanceTimer_Tick;
            _maintenanceTimer.Start();
        }

        public IList<BrowserTab> Tabs
        {
            get { return _tabs; }
        }

        public BrowserTab ActiveTab { get; private set; }

        public int MaxLiveTabs
        {
            get { return _maxLiveTabs; }
        }

        public async Task<BrowserTab> CreateTabAsync(string url)
        {
            BrowserTab tab = new BrowserTab(_nextId++, _environment, _enableDevTools, url);
            tab.TitleChanged += TabTitleChanged;
            tab.NewWindowRequested += TabNewWindowRequested;
            _tabs.Add(tab);
            _host.Controls.Add(tab);
            tab.Dock = DockStyle.Fill;
            // 初始化期间保持可见，避免隐藏父容器导致 WebView2 创建失败；激活时会隐藏其他标签。
            tab.SetContentVisibility(true);

            try
            {
                await tab.EnsureLiveAsync();
            }
            catch (Exception ex)
            {
                Log.Error("[tabs] create failed for tab " + tab.Id, ex);
                await CloseTabAsync(tab.Id);
                throw;
            }

            await ActivateTabAsync(tab.Id);
            await EnforceLiveLimitAsync();

            if (_windowMinimized && tab.IsLive)
            {
                tab.SetContentVisibility(false);
                await tab.SuspendAsync();
            }

            LogTabState("create");
            return tab;
        }

        /// <summary>
        /// 新建标签后立即执行上限检查，避免定时器 5 秒间隔内内存冲高。
        /// </summary>
        private async Task EnforceLiveLimitAsync()
        {
            if (_disposed || _maintenanceBusy)
            {
                return;
            }

            _maintenanceBusy = true;

            try
            {
                if (await TrimToLiveLimitAsync())
                {
                    RaiseTabsChanged();
                    LogTabState("limit");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[tabs] enforce limit failed", ex);
            }
            finally
            {
                _maintenanceBusy = false;
            }
        }

        public async Task ActivateTabAsync(int id)
        {
            BrowserTab tab = Find(id);
            if (tab == null || _disposed)
            {
                return;
            }

            // 激活序号：连续快速点击时只有最后一次激活可以改焦点。
            int version = ++_activationVersion;

            if (tab == ActiveTab)
            {
                if (tab.IsDiscarded)
                {
                    await tab.EnsureLiveAsync();
                }

                if (version != _activationVersion || _disposed || tab.IsDisposed)
                {
                    return;
                }

                tab.SetContentVisibility(true);
                tab.Resume();
                tab.FocusContent();
                RaiseTabsChanged();
                return;
            }

            if (ActiveTab != null)
            {
                ActiveTab.LastDeactivatedUtc = DateTime.UtcNow;
            }

            ActiveTab = tab;
            tab.LastActivatedUtc = DateTime.UtcNow;
            tab.LastDeactivatedUtc = DateTime.MinValue;

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].SetContentVisibility(_tabs[i] == tab);
            }

            if (tab.IsDiscarded)
            {
                try
                {
                    await tab.EnsureLiveAsync();
                }
                catch (Exception ex)
                {
                    Log.Error("[tabs] restore failed for tab " + tab.Id, ex);
                }
            }
            else
            {
                tab.Resume();
            }

            if (version != _activationVersion || _disposed || tab.IsDisposed)
            {
                Log.Debug("[tabs] activation superseded: tab " + tab.Id);
                return;
            }

            tab.FocusContent();
            RaiseActiveChanged();
            RaiseTabsChanged();
            LogTabState("activate");
        }

        public async Task CloseTabAsync(int id)
        {
            BrowserTab tab = Find(id);
            if (tab == null)
            {
                return;
            }

            int index = _tabs.IndexOf(tab);
            bool wasActive = (tab == ActiveTab);

            _tabs.Remove(tab);
            _host.Controls.Remove(tab);
            tab.Dispose();

            if (wasActive)
            {
                ActiveTab = null;

                if (_tabs.Count == 0)
                {
                    RaiseActiveChanged();
                    RaiseTabsChanged();
                    LogTabState("close");

                    EventHandler empty = Empty;
                    if (empty != null)
                    {
                        empty(this, EventArgs.Empty);
                    }

                    return;
                }

                int nextIndex = Math.Min(index, _tabs.Count - 1);
                await ActivateTabAsync(_tabs[nextIndex].Id);
            }

            RaiseTabsChanged();
            LogTabState("close");
        }

        public void ApplyTheme()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].ApplyTheme();
            }
        }

        public async Task ActivateNextAsync(int direction)
        {
            if (_tabs.Count == 0)
            {
                return;
            }

            int index = ActiveTab == null ? 0 : _tabs.IndexOf(ActiveTab);
            int next = (index + direction + _tabs.Count) % _tabs.Count;
            await ActivateTabAsync(_tabs[next].Id);
        }

        public async Task ActivateIndexAsync(int index)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                return;
            }

            await ActivateTabAsync(_tabs[index].Id);
        }

    }
}
