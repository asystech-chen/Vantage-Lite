using System;
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

            if (_contentHost != null)
            {
                _contentHost.BackColor = ThemeManager.Current.WebViewBackground;
            }

            if (_tabManager != null)
            {
                _tabManager.ApplyTheme();
            }
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
