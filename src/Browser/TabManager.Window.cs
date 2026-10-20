using System.Threading.Tasks;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的窗口状态联动：最小化时全量节流，还原时恢复活动标签。
    /// 依赖：BrowserTab 的 Suspend/Resume。改动注意：挂起前必须先把标签设为不可见。
    /// </summary>
    public sealed partial class TabManager
    {
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

    }
}
