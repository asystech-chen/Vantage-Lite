using System;
using System.Drawing;
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

            // Mica 生效时，未绘制区域要留纯黑，DWM 才会透出桌面材质。
            BackColor = WindowChrome.MicaEnabled
                ? System.Drawing.Color.Black
                : ThemeManager.Current.WindowBackground;

            if (_titleBar != null)
            {
                _titleBar.ApplyTheme();
            }

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

            bool privacyDark = ThemeManager.IsDark;

            if (_privacyBanner != null)
            {
                _privacyBanner.BackColor = privacyDark ? Color.FromArgb(23, 42, 69) : Color.FromArgb(216, 232, 252);
                _privacyBanner.Invalidate();
            }

            if (_privacyLabel != null)
            {
                _privacyLabel.BackColor = Color.Transparent;
                _privacyLabel.ForeColor = privacyDark ? Color.White : Color.FromArgb(26, 43, 60);
            }

            if (_privacyExitButton != null)
            {
                _privacyExitButton.UseVisualStyleBackColor = false;
                _privacyExitButton.BackColor = privacyDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(59, 130, 246);
                _privacyExitButton.ForeColor = privacyDark ? Color.FromArgb(11, 31, 51) : Color.White;
                _privacyExitButton.FlatAppearance.MouseOverBackColor = privacyDark ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235);
                _privacyExitButton.FlatAppearance.MouseDownBackColor = privacyDark ? Color.FromArgb(59, 130, 246) : Color.FromArgb(29, 78, 216);
            }

            if (_normalTabManager != null)
            {
                _normalTabManager.ApplyTheme();
            }

            if (_privateTabManager != null)
            {
                _privateTabManager.ApplyTheme();
            }

            if (_tabManager != null)
            {
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
