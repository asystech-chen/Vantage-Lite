using System;
using System.Linq;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的内部辅助：销毁、查找、状态日志、事件转发（partial）。
    /// 依赖：Log / DiagnosticsInfo。改动注意：这里不做挂起/丢弃决策，策略在 TabManager.Maintenance.cs。
    /// </summary>
    public sealed partial class TabManager
    {
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_maintenanceTimer != null)
            {
                _maintenanceTimer.Stop();
                _maintenanceTimer.Dispose();
            }

            for (int i = _tabs.Count - 1; i >= 0; i--)
            {
                _tabs[i].Dispose();
            }

            _tabs.Clear();
            ActiveTab = null;
        }

        private BrowserTab Find(int id)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == id)
                {
                    return _tabs[i];
                }
            }

            return null;
        }

        private void LogTabState(string reason)
        {
            int liveCount = _tabs.Count(t => t.IsLive);
            int suspendedCount = _tabs.Count(t => t.IsSuspended);
            int discardedCount = _tabs.Count(t => t.IsDiscarded);
            string active = ActiveTab == null ? "none" : ActiveTab.Id.ToString();

            Log.Info("[tabs] " + reason
                + ": total=" + _tabs.Count
                + " live=" + liveCount
                + " suspended=" + suspendedCount
                + " discarded=" + discardedCount
                + " active=" + active
                + "; " + DiagnosticsInfo.GetMemorySummary());
        }

        private void TabTitleChanged(object sender, string title)
        {
            // 标题变化只需要刷新标签条，不改变活动标签。
            RaiseTabsChanged();
        }

        private void TabNavigated(object sender, NavigatedEventArgs e)
        {
            EventHandler<NavigatedEventArgs> handler = Navigated;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void TabNewWindowRequested(object sender, string url)
        {
            EventHandler<string> handler = NewWindowRequested;
            if (handler != null)
            {
                handler(this, url);
            }
        }

        private void RaiseTabsChanged()
        {
            EventHandler handler = TabsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseActiveChanged()
        {
            EventHandler handler = ActiveTabChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
