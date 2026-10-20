using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        private static string _mode = "system";

        public static event EventHandler ThemeChanged;

        public static bool IsDark
        {
            get { return _isDark; }
        }

        public static string Mode
        {
            get { return _mode; }
        }

        /// <summary>
        /// 设置主题来源：system / light / dark。手动模式下忽略系统主题变化。
        /// </summary>
        public static void SetMode(string mode)
        {
            string next = NormalizeMode(mode);

            if (string.Equals(next, _mode, StringComparison.Ordinal))
            {
                return;
            }

            _mode = next;
            ApplyResolvedTheme();
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

        /// <summary>
        /// 右键菜单/下拉菜单统一换成主题配色。
        /// 依赖：ToolStripProfessionalRenderer。改动注意：菜单按需创建，每次创建后都要调用。
        /// </summary>
        public static void ApplyMenuTheme(ContextMenuStrip menu)
        {
            if (menu == null)
            {
                return;
            }

            ThemePalette palette = Current;
            menu.Renderer = new ThemedMenuRenderer();
            menu.BackColor = palette.ChromeBackground;
            menu.ForeColor = palette.TextPrimary;
        }

        /// <summary>
        /// 把系统工具提示改成可自绘，颜色在绘制时实时读取主题。
        /// 依赖：ToolTip.OwnerDraw。改动注意：重复调用会被 OwnerDraw 判断挡掉，不会重复挂事件。
        /// </summary>
        public static void ApplyToolTip(ToolTip toolTip)
        {
            if (toolTip == null || toolTip.OwnerDraw)
            {
                return;
            }

            toolTip.OwnerDraw = true;
            toolTip.BackColor = Current.ChromeBackground;
            toolTip.ForeColor = Current.TextPrimary;
            toolTip.Draw += ToolTip_Draw;
            toolTip.Popup += ToolTip_Popup;
        }

        private static void ToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            ThemePalette palette = Current;
            Rectangle bounds = e.Bounds;

            using (SolidBrush background = new SolidBrush(palette.ChromeBackground))
            {
                e.Graphics.FillRectangle(background, bounds);
            }

            if (bounds.Width > 2 && bounds.Height > 2)
            {
                using (Pen border = new Pen(palette.AddressBorder))
                {
                    e.Graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                }
            }

            Rectangle textBounds = new Rectangle(
                bounds.X + 6,
                bounds.Y + 4,
                Math.Max(1, bounds.Width - 12),
                Math.Max(1, bounds.Height - 8));

            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText,
                e.Font,
                textBounds,
                palette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        private static void ToolTip_Popup(object sender, PopupEventArgs e)
        {
            if (e.AssociatedControl == null)
            {
                return;
            }

            Size size = e.ToolTipSize;
            e.ToolTipSize = new Size(size.Width + 12, size.Height + 8);
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!string.Equals(_mode, "system", StringComparison.Ordinal))
            {
                return;
            }

            ApplyResolvedTheme();
        }

        private static void RaiseThemeChanged()
        {
            EventHandler handler = ThemeChanged;

            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private static void ApplyResolvedTheme()
        {
            bool dark = ResolveDark();

            if (dark == _isDark)
            {
                return;
            }

            _isDark = dark;
            RaiseThemeChanged();
        }

        private static bool ResolveDark()
        {
            if (string.Equals(_mode, "dark", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(_mode, "light", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ReadSystemDark();
        }

        private static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase))
            {
                return "dark";
            }

            if (string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase))
            {
                return "light";
            }

            return "system";
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

    /// <summary>
    /// 系统自带图标（文件夹等）。依赖：shell32 / user32。
    /// 改动注意：图标是共享缓存，不要 Dispose；取不到时返回 null，由调用方降级。
    /// </summary>
    internal static class ShellIcons
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiSmallIcon = 0x000000001;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeDirectory = 0x00000010;

        private static Image _folder;
        private static string _folderDataUri;
        private static bool _folderLoaded;

        public static Image Folder()
        {
            if (!_folderLoaded)
            {
                _folderLoaded = true;
                _folder = LoadFolderIcon();
            }

            return _folder;
        }

        public static string FolderDataUri()
        {
            if (_folderDataUri == null)
            {
                Image image = Folder();
                _folderDataUri = string.Empty;

                if (image != null)
                {
                    try
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            image.Save(stream, ImageFormat.Png);
                            _folderDataUri = "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return _folderDataUri;
        }

        private static Image LoadFolderIcon()
        {
            try
            {
                SHFILEINFO info = new SHFILEINFO();
                IntPtr result = SHGetFileInfo(
                    "folder",
                    FileAttributeDirectory,
                    ref info,
                    (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes);

                if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    using (Icon shell = Icon.FromHandle(info.hIcon))
                    {
                        Bitmap bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb);

                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            graphics.DrawIcon(shell, new Rectangle(0, 0, 16, 16));
                        }

                        return bitmap;
                    }
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    /// <summary>
    /// 深/浅主题下的菜单配色表与渲染器。依赖：ThemeManager.Current。
    /// 改动注意：所有颜色实时读取 Current，主题切换后已创建的菜单也能跟着变。
    /// </summary>
    internal sealed class ThemedMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return ThemeManager.Current.ChromeBackground; } }
        public override Color ImageMarginGradientBegin { get { return ThemeManager.Current.ChromeBackground; } }
        public override Color ImageMarginGradientMiddle { get { return ThemeManager.Current.ChromeBackground; } }
        public override Color ImageMarginGradientEnd { get { return ThemeManager.Current.ChromeBackground; } }
        public override Color MenuBorder { get { return ThemeManager.Current.AddressBorder; } }
        public override Color MenuItemBorder { get { return ThemeManager.Current.ButtonHover; } }
        public override Color MenuItemSelected { get { return ThemeManager.Current.ButtonHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return ThemeManager.Current.ButtonHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return ThemeManager.Current.ButtonHover; } }
        public override Color MenuItemPressedGradientBegin { get { return ThemeManager.Current.ButtonPressed; } }
        public override Color MenuItemPressedGradientEnd { get { return ThemeManager.Current.ButtonPressed; } }
        public override Color SeparatorDark { get { return ThemeManager.Current.AddressBorder; } }
        public override Color SeparatorLight { get { return ThemeManager.Current.ChromeBackground; } }
        public override Color CheckBackground { get { return ThemeManager.Current.ButtonHover; } }
        public override Color CheckSelectedBackground { get { return ThemeManager.Current.ButtonPressed; } }
    }

    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemedMenuRenderer()
            : base(new ThemedMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(ThemeManager.Current.ChromeBackground))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            ThemePalette palette = ThemeManager.Current;
            e.TextColor = e.Item.Enabled ? palette.TextPrimary : palette.TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            ThemePalette palette = ThemeManager.Current;
            e.ArrowColor = e.Item.Enabled ? palette.TextPrimary : palette.TextDisabled;
            base.OnRenderArrow(e);
        }
    }

    /// <summary>
    /// 程序自身图标（Vantage）。依赖：System.Drawing / WinForms。
    /// 改动注意：每次返回新 Bitmap，调用方负责释放。
    /// </summary>
    internal static class AppIcon
    {
        public static Image Create16()
        {
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (icon == null)
                    {
                        return null;
                    }

                    using (Bitmap source = icon.ToBitmap())
                    {
                        Bitmap bitmap = new Bitmap(16, 16);

                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(source, 0, 0, 16, 16);
                        }

                        return bitmap;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
