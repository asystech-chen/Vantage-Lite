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
            DateTime now = DateTime.UtcNow;
            bool changed = false;

            // 1) 静默足够久的后台标签 -> 挂起，保留页面状态。
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab tab = _tabs[i];

                if (tab == ActiveTab || !tab.IsLive || tab.IsSuspended || tab.Visible)
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

            // 2) 超过硬上限 -> 按 LRU 销毁，跳过活动标签和脏表单。
            int liveCount = _tabs.Count(t => t.IsLive);
            if (liveCount > _maxLiveTabs)
            {
                List<BrowserTab> candidates = _tabs
                    .Where(t => t != ActiveTab && t.IsLive)
                    .OrderBy(t => t.LastActivatedUtc)
                    .ToList();

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
                    Log.Warn("[tabs] live tabs still above limit; remaining candidates hold unsaved input");
                }
            }

            // 3) 挂起太久 -> 允许真正销毁。
            if (_discardAfterMinutes > 0)
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    BrowserTab tab = _tabs[i];

                    if (tab == ActiveTab || !tab.IsSuspended)
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

        private async Task<bool> TryDiscardAsync(BrowserTab tab)
        {
            bool dirty = tab.IsSuspended ? tab.HasUnsavedInput : await tab.IsDirtyAsync();

            if (dirty)
            {
                Log.Info("[tabs] discard skipped (unsaved input): tab " + tab.Id);
                return false;
            }

            await tab.DiscardAsync();
            return true;
        }
    }
}
