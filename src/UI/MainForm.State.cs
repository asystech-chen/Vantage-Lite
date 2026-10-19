using System.Windows.Forms;
using VantageLite.Browser;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的活动标签状态同步（partial）。
    /// 依赖：NavigationBar / BrowserTab。改动注意：只读标签状态，不改业务数据。
    /// </summary>
    public sealed partial class MainForm
    {
        private void UpdateFromActiveTab()
        {
            BrowserTab tab = ActiveTab;

            if (tab == null)
            {
                Text = "Vantage Lite";
                _navigationBar.SetAddress(string.Empty);
                _navigationBar.SetNavigationState(false, false);
                return;
            }

            _navigationBar.SetAddress(tab.Url);
            _navigationBar.SetNavigationState(tab.CanGoBack, tab.CanGoForward);
            UpdateWindowTitle();
        }

        private void UpdateNavigationState()
        {
            BrowserTab tab = ActiveTab;
            if (tab != null)
            {
                _navigationBar.SetNavigationState(tab.CanGoBack, tab.CanGoForward);
            }
        }

        private void UpdateWindowTitle()
        {
            BrowserTab tab = ActiveTab;
            Text = tab == null ? "Vantage Lite" : tab.DisplayTitle + " - Vantage Lite";
        }
    }
}
