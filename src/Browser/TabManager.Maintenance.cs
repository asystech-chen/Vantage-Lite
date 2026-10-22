using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// 标签维护策略：后台挂起、硬上限销毁、长时间挂起后销毁。
    /// 依赖：BrowserTab 的 Suspend/Resume/DiscardAsync。
    /// 改动注意：活动标签永远跳过；有未提交表单的标签禁止销毁。
    /// </summary>
    public sealed partial class TabManager
    {
        private async void MaintenanceTimer_Tick(object sender, EventArgs e)
        {
            if (_maintenanceBusy)
            {
                return;
            }

            _maintenanceBusy = true;

            try
            {
                await RunMaintenanceAsync();
            }
            catch (Exception ex)
            {
                Log.Error("[tabs] maintenance failed", ex);
            }
            finally
            {
                _maintenanceBusy = false;
            }
        }

        private async Task RunMaintenanceAsync()
        {
            // 工作区被隐私模式切走时不做挂起/丢弃，等恢复后再继续。
            if (!_workspaceActive)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool changed = false;

            // 1) 静默足够久的后台标签 -> 挂起，保留页面状态。
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab tab = _tabs[i];

                if (tab.IsDisposed || tab == ActiveTab || !tab.IsLive || tab.IsSuspended || tab.Visible)
                {
                    continue;
                }

                if (tab.IsPlayingAudio)
                {
                    continue;
                }

                double idleSeconds = (now - tab.LastDeactivatedUtc).TotalSeconds;
                if (_suspendAfterSeconds > 0 && idleSeconds >= _suspendAfterSeconds)
                {
                    if (await tab.SuspendAsync())
                    {
                        changed = true;
                    }
                }
            }

            // 2) 超过硬上限 -> 按 LRU 销毁，跳过活动标签、播放中的页面和脏表单。
            if (await TrimToLiveLimitAsync())
            {
                changed = true;
            }

            // 3) 挂起太久 -> 允许真正销毁。
            if (_discardAfterMinutes > 0)
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    BrowserTab tab = _tabs[i];

                    if (tab.IsDisposed || tab == ActiveTab || !tab.IsSuspended || tab.IsPlayingAudio)
                    {
                        continue;
                    }

                    if ((now - tab.LastDeactivatedUtc).TotalMinutes >= _discardAfterMinutes)
                    {
                        if (await TryDiscardAsync(tab))
                        {
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                RaiseTabsChanged();
                LogTabState("maintenance");
            }
        }

        /// <summary>
        /// 把存活 WebView2 数量压回上限。播放音频的标签和脏表单标签受保护。
        /// </summary>
        private async Task<bool> TrimToLiveLimitAsync()
        {
            int liveCount = _tabs.Count(t => t.IsLive);
            if (liveCount <= _maxLiveTabs)
            {
                return false;
            }

            List<BrowserTab> candidates = _tabs
                .Where(t => !t.IsDisposed && t != ActiveTab && t.IsLive && !t.IsPlayingAudio)
                .OrderBy(t => t.LastActivatedUtc)
                .ToList();

            bool changed = false;

            for (int i = 0; i < candidates.Count && liveCount > _maxLiveTabs; i++)
            {
                if (await TryDiscardAsync(candidates[i]))
                {
                    liveCount--;
                    changed = true;
                }
            }

            if (liveCount > _maxLiveTabs)
            {
                Log.Warn("[tabs] live tabs above limit: " + liveCount
                    + " (protected: audio or unsaved input)");
            }

            return changed;
        }

        private async Task<bool> TryDiscardAsync(BrowserTab tab)
        {
            if (tab.IsDisposed || !tab.IsLive)
            {
                return false;
            }

            if (tab.IsPlayingAudio)
            {
                Log.Debug("[tabs] discard skipped (playing audio): tab " + tab.Id);
                return false;
            }

            bool dirty = tab.IsSuspended ? tab.HasUnsavedInput : await tab.IsDirtyAsync();

            if (tab.IsDisposed || !tab.IsLive)
            {
                return false;
            }

            if (dirty)
            {
                Log.Debug("[tabs] discard skipped (unsaved input): tab " + tab.Id);
                return false;
            }

            await tab.DiscardAsync();
            return true;
        }
    }
}
