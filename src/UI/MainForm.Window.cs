using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VantageLite.App;
using VantageLite.Storage;

namespace VantageLite.UI
{
    /// <summary>
    /// 主窗口的无边框窗体行为与窗口状态持久化（partial）。
    /// 依赖：WindowChrome / WindowStateStore / JsonFile。
    /// 改动注意：Form.Padding 在非最大化时留出缩放边框，最大化时必须清零，否则四周会留白。
    /// </summary>
    public sealed partial class MainForm
    {
        private const int WmNcCalcSize = 0x0083;
        private const int WmNcHitTest = 0x0084;
        private const int WmNcPaint = 0x0085;
        private const int WmNcActivate = 0x0086;
        private const int HtClient = 1;
        private const int HtCaption = 2;

        private bool _restoreMaximized;

        private string WindowStatePath
        {
            get { return Path.Combine(AppPaths.DataDirectory, "window.json"); }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;

                // 必须整体替换样式：WinForms 默认样式里带 WS_BORDER/WS_CAPTION 一类位，
                // 配合 DwmExtendFrameIntoClientArea 会让 DWM 在客户区顶部再画一条经典标题栏。
                const int WsPopup = unchecked((int)0x80000000);
                const int WsClipSiblings = 0x04000000;
                const int WsClipChildren = 0x02000000;
                const int WsSysMenu = 0x00080000;
                const int WsThickFrame = 0x00040000;
                const int WsMinimizeBox = 0x00020000;
                const int WsMaximizeBox = 0x00010000;

                cp.Style = WsPopup | WsSysMenu | WsClipChildren | WsClipSiblings |
                           WsThickFrame | WsMinimizeBox | WsMaximizeBox;

                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            WindowChrome.GlobalDpi = DeviceDpi;
            ApplyChrome();
            ApplyTheme();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            WindowChrome.GlobalDpi = e.DeviceDpiNew;
            UpdateWindowChromeState();

            if (IsHandleCreated)
            {
                // 等布局按新 DPI 重排完再重算玻璃区域。
                BeginInvoke(new Action(ApplyChrome));
            }
        }

        private void ApplyChrome()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            int glassHeight = Padding.Top + (_titleBar == null ? WindowChrome.TitleBarHeight : _titleBar.Height);
            VantageLite.Diagnostics.Log.Info("dpi=" + DeviceDpi + " titleBar=" + (_titleBar == null ? 0 : _titleBar.Height) +
                " navBar=" + (_navigationBar == null ? 0 : _navigationBar.Height) +
                " bookmarksBar=" + (_bookmarksBar == null ? 0 : _bookmarksBar.Height));
            WindowChrome.Apply(this, glassHeight, _settings != null && _settings.EnableMica);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcCalcSize && m.WParam != IntPtr.Zero)
            {
                // 客户区铺满整个窗口，不再保留系统标题栏和边框。
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmNcPaint)
            {
                // 失去激活时系统会重画非客户区，不加这句会冒出一圈边框。
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmNcActivate)
            {
                // 让激活状态变化完全不碰非客户区（避免重新画出标题栏或边框）。
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WmNcHitTest)
            {
                long value = m.LParam.ToInt64();
                int x = unchecked((short)(value & 0xFFFF));
                int y = unchecked((short)((value >> 16) & 0xFFFF));
                int captionHeight = Padding.Top + (_titleBar == null ? 0 : _titleBar.Height);
                int hit = WindowChrome.HitTest(this, x, y, captionHeight);

                // 顶边 5px 默认是 HTCAPTION；但如果压在标签或窗口按钮上，必须让点击落回子控件。
                if (hit == HtCaption && _titleBar != null &&
                    _titleBar.IsInteractiveAt(new Point(x, y)))
                {
                    hit = HtClient;
                }

                if (hit != HtClient)
                {
                    m.Result = (IntPtr)hit;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_restoreMaximized)
            {
                _restoreMaximized = false;
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }

            UpdateWindowChromeState();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateWindowChromeState();
        }

        private void UpdateWindowChromeState()
        {
            if (_titleBar == null || _titleBar.IsDisposed)
            {
                return;
            }

            bool maximized = WindowState == FormWindowState.Maximized;
            int border = maximized ? 0 : WindowChrome.GetResizeBorder(this);

            if (Padding.Top != 0 || Padding.Left != border)
            {
                Padding = new Padding(border, 0, border, border);
            }

            if (!maximized && IsHandleCreated)
            {
                // 记住当前显示器的工作区，最大化时不会盖住任务栏。
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            }

            _titleBar.SetMaximized(maximized);
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                return;
            }

            if (IsHandleCreated)
            {
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            }

            WindowState = FormWindowState.Maximized;
        }

        private void RestoreWindowState()
        {
            WindowStateData data = JsonFile.Load<WindowStateData>(WindowStatePath, null);
            Rectangle bounds = Rectangle.Empty;

            if (data != null && data.Width > 0 && data.Height > 0)
            {
                bounds = new Rectangle(
                    data.X,
                    data.Y,
                    Math.Max(MinimumSize.Width, data.Width),
                    Math.Max(MinimumSize.Height, data.Height));
            }

            if (bounds.IsEmpty || !IsVisibleOnAnyScreen(bounds))
            {
                bounds = CenteredDefaultBounds();
                data = null;
            }

            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            _restoreMaximized = data != null && data.Maximized;
        }

        private void SaveWindowState()
        {
            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            JsonFile.Save(WindowStatePath, new WindowStateData
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = WindowState == FormWindowState.Maximized
            });
        }

        private Rectangle CenteredDefaultBounds()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            int width = Math.Min(1200, area.Width);
            int height = Math.Min(800, area.Height);

            return new Rectangle(
                area.Left + (area.Width - width) / 2,
                area.Top + (area.Height - height) / 2,
                width,
                height);
        }

        /// <summary>
        /// 至少要有 120×60 落在某块屏幕的工作区内才算可见，避免显示器拔掉后窗口跑到屏幕外。
        /// </summary>
        private static bool IsVisibleOnAnyScreen(Rectangle bounds)
        {
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                Rectangle visible = Rectangle.Intersect(Screen.AllScreens[i].WorkingArea, bounds);

                if (visible.Width >= 120 && visible.Height >= 60)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
