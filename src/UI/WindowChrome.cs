using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 无边框窗口的命中测试与 DWM 效果（Mica / 圆角 / 阴影）。
    /// 依赖：dwmapi / user32 / ThemeManager。
    /// 改动注意：Mica 生效时背景色必须画纯黑，DWM 会把扩展帧区域里的纯黑当作透明。
    /// </summary>
    internal static class WindowChrome
    {
        public const int ResizeBorder = 5;
        public const int TitleBarHeight = 36;

        private const int WmNcLButtonDown = 0x00A1;

        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        private const int DwmWindowCornerPreference = 33;
        private const int DwmSystemBackdropType = 38;

        private const int DwmsbtNone = 1;
        private const int DwmsbtMainWindow = 2;
        private const int DwmwcpRound = 2;

        /// <summary>Mica 是否已成功启用；失败时退回主题纯色。</summary>
        public static bool MicaEnabled { get; private set; }

        /// <summary>
        /// 标题栏/标签条的底色。Mica 生效时用纯黑，让 DWM 透出桌面材质。
        /// </summary>
        public static Color BackdropColor
        {
            get { return MicaEnabled ? Color.Black : ThemeManager.Current.TabStripBackground; }
        }

        /// <summary>
        /// 启用 DWM 效果。glassHeight 是顶部要透出 Mica 的高度（客户区坐标）。
        /// </summary>
        public static void Apply(Form form, int glassHeight, bool allowMica)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }

            bool mica = false;
            int build = Environment.OSVersion.Version.Build;

            if (build >= 22000)
            {
                // 去掉 DWM 自带的窗口边框（未激活时会明显显示出来）。
                int noBorder = unchecked((int)0xFFFFFFFE);
                DwmSetWindowAttribute(form.Handle, 34, ref noBorder, sizeof(int));

                // 圆角与 Mica 独立：即使不开材质也尽量贴合 Win11 窗口风格。
                int corners = DwmwcpRound;
                DwmSetWindowAttribute(form.Handle, DwmWindowCornerPreference, ref corners, sizeof(int));
            }

            if (!allowMica)
            {
                // 默认不碰 DWM 扩展帧：正边距会把帧材质（系统强调色）画进客户区。
                if (MicaEnabled)
                {
                    Reset(form);
                }

                MicaEnabled = false;
                return;
            }

            Log.Info("WindowChrome: style=0x" + GetWindowLong(form.Handle, -16).ToString("X8") + " build=" + build);

            if (allowMica && build >= 22621)
            {
                int backdrop = DwmsbtMainWindow;
                int hrBackdrop = DwmSetWindowAttribute(form.Handle, DwmSystemBackdropType, ref backdrop, sizeof(int));

                if (hrBackdrop == 0)
                {
                    int corners = DwmwcpRound;
                    DwmSetWindowAttribute(form.Handle, DwmWindowCornerPreference, ref corners, sizeof(int));

                    MARGINS margins = new MARGINS();
                    margins.cyTopHeight = glassHeight;

                    int hrFrame = DwmExtendFrameIntoClientArea(form.Handle, ref margins);
                    mica = hrFrame == 0;
                    Log.Info("WindowChrome: backdrop hr=0x" + hrBackdrop.ToString("X") +
                        " frame hr=0x" + hrFrame.ToString("X") + " glass=" + glassHeight);
                }
                else
                {
                    Log.Warn("WindowChrome: backdrop attribute failed hr=0x" + hrBackdrop.ToString("X") +
                        " build=" + build);
                }
            }

            if (!mica)
            {
                Reset(form);
            }

            MicaEnabled = mica;
        }

        private static void Reset(Form form)
        {
            int none = DwmsbtNone;
            DwmSetWindowAttribute(form.Handle, DwmSystemBackdropType, ref none, sizeof(int));

            MARGINS margins = new MARGINS();
            DwmExtendFrameIntoClientArea(form.Handle, ref margins);
        }

        /// <summary>缩放边框宽度：按当前 DPI 换算，保证高 DPI 下仍好抓。</summary>
        public static int GetResizeBorder(Control control)
        {
            int dpi = control == null ? 96 : control.DeviceDpi;
            int scaled = (int)Math.Round(ResizeBorder * dpi / 96.0);
            return Math.Max(4, scaled);
        }

        /// <summary>
        /// 窗口边缘返回缩放命中码，标题栏空白返回 HTCAPTION，其余交给控件。
        /// </summary>
        public static int HitTest(Form form, int screenX, int screenY, int captionHeight)
        {
            if (form == null)
            {
                return HtClient;
            }

            Rectangle bounds = form.Bounds;

            if (form.WindowState == FormWindowState.Maximized)
            {
                return screenY < bounds.Top + captionHeight ? HtCaption : HtClient;
            }

            int border = form.Padding.Left;

            if (border <= 0)
            {
                border = ResizeBorder;
            }

            bool left = screenX < bounds.Left + border;
            bool right = screenX >= bounds.Right - border;
            bool top = screenY < bounds.Top + border;
            bool bottom = screenY >= bounds.Bottom - border;

            if (top && left) return HtTopLeft;
            if (top && right) return HtTopRight;
            if (bottom && left) return HtBottomLeft;
            if (bottom && right) return HtBottomRight;
            if (left) return HtLeft;
            if (right) return HtRight;
            // 顶边整条当标题栏用（标签栏满时仍能拖动窗口），缩放交给四角和左右/下边。
            if (top) return HtCaption;
            if (bottom) return HtBottom;

            return screenY < bounds.Top + captionHeight ? HtCaption : HtClient;
        }

        /// <summary>
        /// 从标签条空白处开始拖动窗口。走系统非客户区拖动，保留贴边分屏与最大化拖拽还原。
        /// </summary>
        public static void BeginDrag(Form form)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(form.Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
