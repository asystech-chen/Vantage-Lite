using System.Threading.Tasks;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的窗口状态联动：最小化时全量节流，还原时恢复活动标签。
    /// 依赖：BrowserTab 的 Suspend/Resume。改动注意：挂起前必须先把标签设为不可见。
    /// </summary>
    public sealed partial class TabManager
    {
        private bool _workspaceActive = true;

        /// <summary>
        /// 窗口最小化时把所有标签（含活动标签）挂起；还原时恢复活动标签。
        /// 注意：WebView2 以控件可见性判断能否挂起，所以必须先隐藏所有标签。
        /// </summary>
        public async Task SetMinimizedAsync(bool minimized)
        {
            if (_disposed || _windowMinimized == minimized)
            {
                return;
            }

            _windowMinimized = minimized;

            if (!minimized)
            {
                // 工作区被切走时保持隐藏，等 SetWorkspaceActiveAsync(true) 再恢复。
                if (!_workspaceActive)
                {
                    RaiseTabsChanged();
                    LogTabState("restore-skipped");
                    return;
                }

                BrowserTab active = ActiveTab;
                if (active != null && !active.IsDisposed)
                {
                    active.SetContentVisibility(true);

                    if (active.IsDiscarded)
                    {
                        await active.EnsureLiveAsync();
                    }
                    else
                    {
                        active.Resume();
                    }

                    active.FocusContent();
                }

                RaiseTabsChanged();
                LogTabState("restore");
                return;
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].SetContentVisibility(false);
            }

            // 给 WebView2 一点时间处理隐藏，否则 TrySuspendAsync 会因“仍可见”被拒绝。
            await Task.Delay(50);

            for (int i = 0; i < _tabs.Count && _windowMinimized; i++)
            {
                BrowserTab tab = _tabs[i];

                if (tab.IsDisposed || !tab.IsLive || tab.IsSuspended)
                {
                    continue;
                }

                await tab.SuspendAsync();
            }

            RaiseTabsChanged();
            LogTabState("minimize");
        }

        /// <summary>
        /// 隐私工作区切换：把整组标签隐藏/挂起，或恢复活动标签。
        /// 与最小化不同，隐藏期间暂停维护定时器，避免普通标签被 LRU 回收。
        /// </summary>
        public async Task SetWorkspaceActiveAsync(bool active)
        {
            if (_disposed || _workspaceActive == active)
            {
                return;
            }

            _workspaceActive = active;

            if (active)
            {
                _maintenanceTimer.Start();

                BrowserTab activeTab = ActiveTab;

                if (!_windowMinimized && activeTab != null && !activeTab.IsDisposed)
                {
                    activeTab.SetContentVisibility(true);

                    if (activeTab.IsDiscarded)
                    {
                        await activeTab.EnsureLiveAsync();
                    }
                    else
                    {
                        activeTab.Resume();
                    }

                    activeTab.FocusContent();
                }

                RaiseTabsChanged();
                LogTabState("workspace-show");
                return;
            }

            _maintenanceTimer.Stop();

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].SetContentVisibility(false);
            }

            // 与最小化一致：先隐藏，再等 WebView2 处理可见性变化。
            await Task.Delay(50);

            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab tab = _tabs[i];

                if (tab.IsDisposed || !tab.IsLive || tab.IsSuspended)
                {
                    continue;
                }

                await tab.SuspendAsync();
            }

            RaiseTabsChanged();
            LogTabState("workspace-hide");
        }

        /// <summary>
        /// 隐私工作区停留较久后回收普通工作区的空闲标签，跳过脏表单和播放中的页面。
        /// </summary>
        public async Task<int> ReclaimWorkspaceMemoryAsync()
        {
            if (_disposed || _workspaceActive)
            {
                return 0;
            }

            int discarded = 0;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_workspaceActive)
                {
                    break;
                }

                BrowserTab tab = _tabs[i];

                if (tab.IsDisposed || !tab.IsLive || tab.IsDiscarded || tab.IsPlayingAudio)
                {
                    continue;
                }

                bool dirty = tab.IsSuspended ? tab.HasUnsavedInput : await tab.IsDirtyAsync();

                if (tab.IsDisposed || tab.IsDiscarded || dirty)
                {
                    continue;
                }

                if (await tab.DiscardAsync())
                {
                    discarded++;
                }
            }

            if (discarded > 0)
            {
                RaiseTabsChanged();
                LogTabState("workspace-reclaim");
            }

            return discarded;
        }

    }
}
