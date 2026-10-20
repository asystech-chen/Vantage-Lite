using System;
using VantageLite.Browser;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的主题初始化与切换（partial）。
    /// 依赖：ThemeManager。改动注意：系统主题事件可能来自非 UI 线程，必须切回 UI 线程再重绘。
    /// </summary>
    public sealed partial class MainForm
    {
        private void InitializeTheme()
        {
            ThemeManager.Initialize();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
            HandleCreated += delegate { ApplyTheme(); };
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyWindowTheme(this);

            if (_navigationBar != null)
            {
                _navigationBar.ApplyTheme();
            }

            if (_tabStrip != null)
            {
                _tabStrip.ApplyTheme();
            }

            if (_bookmarksBar != null)
            {
                _bookmarksBar.ApplyTheme();
            }

            if (_contentHost != null)
            {
                _contentHost.BackColor = ThemeManager.Current.WebViewBackground;
            }

            if (_tabManager != null)
            {
                _tabManager.ApplyTheme();
                ApplyInternalPageThemes();
            }
        }

        /// <summary>
        /// 内置页用 CSS 变量着色；主题切换时逐个注入脚本，不必刷新页面。
        /// </summary>
        private void ApplyInternalPageThemes()
        {
            if (_tabManager == null)
            {
                return;
            }

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                ApplyInternalPageTheme(_tabManager.Tabs[i]);
            }
        }

        private static void ApplyInternalPageTheme(BrowserTab tab)
        {
            if (tab == null || !tab.IsLive)
            {
                return;
            }

            if (!IsInternalAddress(tab.Url))
            {
                return;
            }

            tab.ExecuteScript(InternalPages.ThemeScript());
        }

        private static bool IsInternalAddress(string url)
        {
            return string.Equals(url, NewTabAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(url, HistoryAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(url, DownloadsAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(url, BookmarksAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(url, AboutAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(url, PreferencesAddress, StringComparison.OrdinalIgnoreCase);
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTheme));
                return;
            }

            ApplyTheme();
        }

        private void ShutdownTheme()
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        }
    }
}
