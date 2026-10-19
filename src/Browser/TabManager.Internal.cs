using System;
using System.Linq;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的内部辅助：销毁、查找、LRU 丢弃、状态日志（partial）。
    /// 依赖：LINQ / Log / DiagnosticsInfo。改动注意：丢弃候选永远排除活动标签。
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

        private void EnforceLimit()
        {
            int liveCount = _tabs.Count(t => t.IsLive);

            while (liveCount > _maxLiveTabs)
            {
                BrowserTab candidate = _tabs
                    .Where(t => t.IsLive && t != ActiveTab)
                    .OrderBy(t => t.LastActivatedUtc)
                    .FirstOrDefault();

                if (candidate == null)
                {
                    break;
                }

                candidate.Discard();
                liveCount--;
            }
        }

        private void LogTabState(string reason)
        {
            int liveCount = _tabs.Count(t => t.IsLive);
            string active = ActiveTab == null ? "none" : ActiveTab.Id.ToString();

            Log.Info("[tabs] " + reason
                + ": total=" + _tabs.Count
                + " live=" + liveCount
                + " active=" + active
                + "; " + DiagnosticsInfo.GetMemorySummary());
        }

        private void TabTitleChanged(object sender, string title)
        {
            // 标题变化只需要刷新标签条，不改变活动标签。
            RaiseTabsChanged();
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
