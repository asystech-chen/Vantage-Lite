using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VantageLite.Theming
{
    /// <summary>
    /// 跟随 Windows 应用主题（亮/暗），并在切换时通知界面重绘。
    /// 依赖：注册表 + SystemEvents + dwmapi。
    /// 改动注意：ThemeChanged 可能来自非 UI 线程，订阅方必须自己切回 UI 线程。
    /// </summary>
    public static class ThemeManager
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private static bool _initialized;
        private static bool _isDark;

        public static event EventHandler ThemeChanged;

        public static bool IsDark
        {
            get { return _isDark; }
        }

        public static ThemePalette Current
        {
            get { return _isDark ? ThemePalette.Dark : ThemePalette.Light; }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _isDark = ReadSystemDark();

            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            catch
            {
                // 极少数环境拿不到系统事件，退化为只在启动时读一次。
            }
        }

        /// <summary>
        /// 设置窗口背景色，并让原生标题栏跟随明暗主题。
        /// </summary>
        public static void ApplyWindowTheme(Form form)
        {
            if (form == null)
            {
                return;
            }

            form.BackColor = Current.WindowBackground;

            if (!form.IsHandleCreated)
            {
                return;
            }

            int dark = _isDark ? 1 : 0;
            const int immersiveDarkMode = 20;
            const int immersiveDarkModeOld = 19;

            try
            {
                if (DwmSetWindowAttribute(form.Handle, immersiveDarkMode, ref dark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, immersiveDarkModeOld, ref dark, sizeof(int));
                }
            }
            catch
            {
            }
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            bool dark = ReadSystemDark();
            if (dark == _isDark)
            {
                return;
            }

            _isDark = dark;

            EventHandler handler = ThemeChanged;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private static bool ReadSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue("AppsUseLightTheme");
                    if (value is int)
                    {
                        return (int)value == 0;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int value,
            int size);
    }
}
