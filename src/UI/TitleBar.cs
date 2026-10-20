using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 自绘标题栏：左侧是标签条，右侧是最小化/最大化/关闭。
    /// 依赖：TabStrip / ThemeManager / WindowChrome。
    /// 改动注意：窗口按钮由本控件直接绘制（不是子控件），否则拿不到非客户区命中测试，
    /// Win11 的 Snap Layouts 不会出现。
    /// </summary>
    public sealed class TitleBar : UserControl
    {
        public const int ButtonWidth = 46;
        private const int ButtonCount = 3;
        private const int MinimizeIndex = 0;
        private const int MaximizeIndex = 1;
        private const int CloseIndex = 2;

        private const int WmNcMouseMove = 0x00A0;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcMouseLeave = 0x02A2;
        private const int WmNcHitTest = 0x0084;
        private const int HtMaxButton = 9;

        private const int TmeNonClient = 0x00000002;
        private const int TmeLeave = 0x00000010;

        // Segoe Fluent Icons / Segoe MDL2 Assets 通用字形。
        private const string GlyphMinimize = "\uE921";
        private const string GlyphMaximize = "\uE922";
        private const string GlyphRestore = "\uE923";
        private const string GlyphClose = "\uE8BB";

        private readonly TabStrip _tabs;
        private readonly Font _glyphFont;
        private int _hover = -1;
        private int _pressed = -1;
        private bool _trackingLeave;
        private bool _maximized;

        public event EventHandler MinimizeRequested;
        public event EventHandler MaximizeToggleRequested;
        public event EventHandler CloseRequested;

        public TitleBar()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            Height = WindowChrome.TitleBarHeight;
            Padding = new Padding(0, 0, ButtonWidth * ButtonCount, 0);
            BackColor = WindowChrome.BackdropColor;

            _glyphFont = IconFont.CreatePx(13f);

            _tabs = new TabStrip();
            _tabs.Dock = DockStyle.Fill;
            Controls.Add(_tabs);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyMetrics();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyMetrics();
        }

        private void ApplyMetrics()
        {
            Height = WindowChrome.Scale(this, WindowChrome.TitleBarHeight);
            Padding = new Padding(0, 0, WindowChrome.Scale(this, ButtonWidth) * ButtonCount, 0);
            Invalidate();
        }

        /// <summary>标签条本体，MainForm 直接接线。</summary>
        public TabStrip Tabs
        {
            get { return _tabs; }
        }

        /// <summary>
        /// 屏幕坐标是否落在标签、新建按钮或窗口控制按钮上。
        /// </summary>
        public bool IsInteractiveAt(Point screenPoint)
        {
            return (_tabs != null && _tabs.IsInteractiveAt(screenPoint)) ||
                   HitButton(PointToClient(screenPoint)) != -1;
        }

        public void ApplyTheme()
        {
            BackColor = WindowChrome.BackdropColor;

            if (_tabs != null)
            {
                _tabs.ApplyTheme();
            }

            Invalidate();
        }

        public void SetMaximized(bool maximized)
        {
            if (_maximized == maximized)
            {
                return;
            }

            _maximized = maximized;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _glyphFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color backdrop = WindowChrome.BackdropColor;
            ThemePalette palette = ThemeManager.Current;

            using (SolidBrush brush = new SolidBrush(backdrop))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            for (int i = 0; i < ButtonCount; i++)
            {
                Rectangle rect = ButtonRect(i);
                bool hovered = _hover == i;

                if (hovered)
                {
                    Color fill = i == CloseIndex
                        ? (_pressed == i ? ControlPaint.Dark(palette.CloseHover, 0.1f) : palette.CloseHover)
                        : (_pressed == i ? palette.CaptionPressed : palette.CaptionHover);

                    using (SolidBrush brush = new SolidBrush(fill))
                    {
                        e.Graphics.FillRectangle(brush, rect);
                    }
                }

                Color glyph = hovered && i == CloseIndex ? Color.White : palette.TextPrimary;

                TextRenderer.DrawText(
                    e.Graphics,
                    GlyphFor(i),
                    _glyphFont,
                    rect,
                    glyph,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = HitButton(e.Location);

            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hover != -1 || _pressed != -1)
            {
                _hover = -1;
                _pressed = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _pressed = HitButton(e.Location);

            if (_pressed != -1)
            {
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = HitButton(e.Location);
            int pressed = _pressed;
            _pressed = -1;
            Invalidate();

            if (index != -1 && index == pressed)
            {
                RaiseFor(index);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                // 只有最大化按钮走非客户区，Windows 11 才会在悬停时弹出贴靠布局。
                if (HitButton(PointToClient(ScreenPointFromLParam(m.LParam))) == MaximizeIndex)
                {
                    m.Result = (IntPtr)HtMaxButton;
                    return;
                }
            }
            else if (m.Msg == WmNcLButtonDown && m.WParam.ToInt32() == HtMaxButton)
            {
                RaiseFor(MaximizeIndex);
                return;
            }
            else if (m.Msg == WmNcMouseMove && m.WParam.ToInt32() == HtMaxButton)
            {
                if (_hover != MaximizeIndex)
                {
                    _hover = MaximizeIndex;
                    Invalidate();
                }

                TrackLeave();
                return;
            }
            else if (m.Msg == WmNcMouseLeave)
            {
                _trackingLeave = false;

                if (_hover != -1)
                {
                    _hover = -1;
                    Invalidate();
                }
            }

            base.WndProc(ref m);
        }

        private string GlyphFor(int index)
        {
            if (index == MinimizeIndex)
            {
                return GlyphMinimize;
            }

            if (index == MaximizeIndex)
            {
                return _maximized ? GlyphRestore : GlyphMaximize;
            }

            return GlyphClose;
        }

        private Rectangle ButtonRect(int index)
        {
            int width = ButtonWidthFromPadding();

            return new Rectangle(
                Width - (ButtonCount - index) * width,
                0,
                width,
                Height);
        }

        private int ButtonWidthFromPadding()
        {
            int width = Padding.Right / ButtonCount;
            return width > 0 ? width : ButtonWidth;
        }

        private int HitButton(Point point)
        {
            int width = ButtonWidthFromPadding();
            int start = Width - ButtonCount * width;

            if (point.X < start || point.X >= Width || point.Y < 0 || point.Y >= Height)
            {
                return -1;
            }

            int index = (point.X - start) / width;
            return index < 0 || index >= ButtonCount ? -1 : index;
        }

        private void RaiseFor(int index)
        {
            EventHandler handler =
                index == MinimizeIndex ? MinimizeRequested :
                index == MaximizeIndex ? MaximizeToggleRequested :
                index == CloseIndex ? CloseRequested : null;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void TrackLeave()
        {
            if (_trackingLeave)
            {
                return;
            }

            _trackingLeave = true;

            TRACKMOUSEEVENT track = new TRACKMOUSEEVENT();
            track.cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT));
            track.dwFlags = TmeNonClient | TmeLeave;
            track.hwndTrack = Handle;

            try
            {
                TrackMouseEvent(ref track);
            }
            catch
            {
                _trackingLeave = false;
            }
        }

        private static Point ScreenPointFromLParam(IntPtr lParam)
        {
            long value = lParam.ToInt64();
            return new Point(unchecked((short)(value & 0xFFFF)), unchecked((short)((value >> 16) & 0xFFFF)));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TRACKMOUSEEVENT
        {
            public int cbSize;
            public int dwFlags;
            public IntPtr hwndTrack;
            public int dwHoverTime;
        }

        [DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT trackMouseEvent);
    }
}
