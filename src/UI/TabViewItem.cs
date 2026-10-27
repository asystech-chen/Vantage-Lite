using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 单个标签按钮：标题、关闭按钮、活动指示线。
    /// 依赖：BrowserTab（只读展示）/ ThemeManager。
    /// 改动注意：标题用 TextRenderer 自绘，避免 Label.AutoEllipsis 在溢出时改变垂直位置。
    /// </summary>
    internal sealed class TabViewItem : Panel
    {
        private readonly Button _closeButton;
        private readonly Button _audioButton;
        private readonly Panel _indicator;
        private readonly Timer _spinner;
        private readonly Font _pinFont;

        private BrowserTab _tab;
        private bool _active;
        private bool _hovered;
        private bool _loading;
        private int _spinAngle;
        private string _titleText;
        private Color _titleColor;
        private int _fadeLeft;
        private int _fadeRight;
        private bool _suppressSelect;

        public TabViewItem(int tabId)
        {
            TabId = tabId;

            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.StandardDoubleClick,
                true);

            Width = 180;
            Height = 30;
            Margin = new Padding(0, 0, 2, 0);
            Font = ThemeFonts.Ui(9F);
            Cursor = Cursors.Default;

            _pinFont = IconFont.CreatePx(10f);

            _spinner = new Timer();
            _spinner.Interval = 90;
            _spinner.Tick += delegate
            {
                _spinAngle = (_spinAngle + 30) % 360;

                if (_loading)
                {
                    Invalidate();
                }
            };

            _indicator = new ClickThroughPanel();
            _indicator.Dock = DockStyle.Top;
            _indicator.Height = 2;

            _closeButton = new FlatButton();
            _closeButton.Text = IconFont.Close;
            _closeButton.Dock = DockStyle.Right;
            _closeButton.Width = 22;
            _closeButton.FlatStyle = FlatStyle.Flat;
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Font = IconFont.CreatePx(11f);
            _closeButton.TabStop = false;
            _closeButton.Cursor = Cursors.Default;
            _closeButton.FlatAppearance.MouseOverBackColor = ThemeManager.Current.CloseHover;
            _closeButton.FlatAppearance.MouseDownBackColor = ThemeManager.Current.CloseHover;
            _closeButton.Click += delegate { Raise(CloseRequested); };
            _closeButton.MouseEnter += OnCloseMouseEnter;
            _closeButton.MouseLeave += OnCloseMouseLeave;

            _audioButton = new FlatButton();
            _audioButton.Text = IconFont.Volume;
            _audioButton.Width = 20;
            _audioButton.Height = Height;
            _audioButton.FlatStyle = FlatStyle.Flat;
            _audioButton.FlatAppearance.BorderSize = 0;
            _audioButton.Font = IconFont.CreatePx(11f);
            _audioButton.TabStop = false;
            _audioButton.Visible = false;
            _audioButton.Cursor = Cursors.Default;
            _audioButton.Click += delegate { Raise(MuteToggleRequested); };

            Controls.Add(_audioButton);
            Controls.Add(_closeButton);
            Controls.Add(_indicator);
            // 指示条必须盖在喇叭/关闭按钮之上，否则播放音频时顶部蓝条被按钮遮住。
            _indicator.BringToFront();

            Click += delegate
            {
                if (_suppressSelect)
                {
                    _suppressSelect = false;
                    return;
                }

                Raise(SelectRequested);
            };
            DoubleClick += delegate { Raise(DoubleClickRequested); };
            MouseDown += OnMouseDown;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            MouseMove += delegate { UpdateCloseVisibility(); };

            ApplyTheme();
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
            if (_closeButton != null)
            {
                _closeButton.Width = WindowChrome.Scale(this, 22);
            }

            if (_audioButton != null)
            {
                _audioButton.Width = WindowChrome.Scale(this, 20);
            }

            if (_indicator != null)
            {
                _indicator.Height = WindowChrome.Scale(this, 2);
            }

            Invalidate();
        }

        public int TabId { get; private set; }

        /// <summary>
        /// 拖拽排序结束后，抑制本次 MouseUp 顺带触发的选中。
        /// </summary>
        public void SuppressNextSelect()
        {
            _suppressSelect = true;
        }

        public event EventHandler SelectRequested;
        public event EventHandler CloseRequested;
        public event EventHandler MiddleCloseRequested;
        public event EventHandler DoubleClickRequested;
        public event EventHandler MuteToggleRequested;

        public void Update(BrowserTab tab, bool active)
        {
            if (!ReferenceEquals(_tab, tab))
            {
                if (_tab != null)
                {
                    _tab.FaviconChanged -= Tab_FaviconChanged;
                    _tab.LoadingChanged -= Tab_LoadingChanged;
                    _tab.AudioStateChanged -= Tab_AudioStateChanged;
                }

                _tab = tab;

                if (_tab != null)
                {
                    _tab.FaviconChanged += Tab_FaviconChanged;
                    _tab.LoadingChanged += Tab_LoadingChanged;
                    _tab.AudioStateChanged += Tab_AudioStateChanged;
                }
            }

            _active = active;
            SetLoading(tab != null && tab.IsLoading);
            UpdateVisual();
            UpdateAudioButton();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tab != null)
                {
                    _tab.FaviconChanged -= Tab_FaviconChanged;
                    _tab.LoadingChanged -= Tab_LoadingChanged;
                    _tab.AudioStateChanged -= Tab_AudioStateChanged;
                    _tab = null;
                }

                _spinner.Dispose();
                _pinFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Tab_FaviconChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void Tab_LoadingChanged(object sender, bool loading)
        {
            SetLoading(loading);
        }

        private void Tab_AudioStateChanged(object sender, EventArgs e)
        {
            UpdateAudioButton();
            Invalidate();
        }

        private void UpdateAudioButton()
        {
            if (_audioButton == null)
            {
                return;
            }

            bool audible = _tab != null && (_tab.IsPlayingAudio || _tab.IsMuted);
            _audioButton.Visible = audible;

            if (!audible)
            {
                return;
            }

            bool muted = _tab.IsMuted;
            _audioButton.Text = muted ? IconFont.VolumeMuted : IconFont.Volume;
            _audioButton.ForeColor = muted
                ? ThemeManager.Current.TextPrimary
                : ThemeManager.Current.Accent;
            _audioButton.FlatAppearance.MouseOverBackColor = ThemeManager.Current.ButtonHover;

            int width = WindowChrome.Scale(this, 20);
            int closeWidth = _closeButton == null ? 0 : _closeButton.Width;
            _audioButton.Width = width;
            _audioButton.Height = Height;
            _audioButton.Left = Math.Max(0, Width - closeWidth - width);
            _audioButton.Top = 0;
        }

        private void SetLoading(bool loading)
        {
            if (_loading == loading)
            {
                return;
            }

            _loading = loading;

            if (loading)
            {
                _spinner.Start();
            }
            else
            {
                _spinner.Stop();
            }

            Invalidate();
        }

        public void ApplyTheme()
        {
            UpdateVisual();
            UpdateAudioButton();
        }

        /// <summary>
        /// 设置左右两端被标签条视口裁切时的渐变宽度（像素）。
        /// </summary>
        public void SetFade(int left, int right)
        {
            if (_fadeLeft == left && _fadeRight == right)
            {
                return;
            }

            _fadeLeft = left;
            _fadeRight = right;
            UpdateCloseVisibility();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            UpdateCloseVisibility();
            UpdateAudioButton();
            Invalidate();
        }

        private void UpdateCloseVisibility()
        {
            if (_closeButton == null)
            {
                return;
            }

            // 宽标签一直显示；窄标签（溢出时）鼠标移上去才显示，避免常驻挤掉标题。
            bool over = ClientRectangle.Contains(PointToClient(Cursor.Position));
            _closeButton.Visible =
                (Width >= WindowChrome.Scale(this, 88) || over) &&
                _fadeLeft + _fadeRight < Width - WindowChrome.Scale(this, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int rightPadding = (_closeButton != null && _closeButton.Visible)
                ? _closeButton.Width + WindowChrome.Scale(this, 14)
                : WindowChrome.Scale(this, 10);

            if (_audioButton != null && _audioButton.Visible)
            {
                rightPadding += _audioButton.Width;
            }

            int iconSize = WindowChrome.Scale(this, 16);
            int left = WindowChrome.Scale(this, 8);
            Image icon = _tab == null ? null : _tab.Favicon;

            if (_loading)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (Pen pen = new Pen(ThemeManager.Current.Accent, 2F))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    e.Graphics.DrawArc(pen, left + 1, (Height - iconSize) / 2 + 1,
                        iconSize - 2, iconSize - 2, _spinAngle, 260);
                }
            }
            else if (icon != null)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(icon, new Rectangle(left, (Height - iconSize) / 2, iconSize, iconSize));
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(ThemeManager.Current.TextDisabled))
                {
                    int dot = WindowChrome.Scale(this, 6);
                    e.Graphics.FillEllipse(brush, left + WindowChrome.Scale(this, 5), (Height - dot) / 2, dot, dot);
                }
            }

            left += iconSize + WindowChrome.Scale(this, 6);

            if (_tab != null && _tab.IsPinned)
            {
                int pinWidth = WindowChrome.Scale(this, 12);
                TextRenderer.DrawText(
                    e.Graphics,
                    IconFont.Pin,
                    _pinFont,
                    new Rectangle(left, 0, pinWidth, Height),
                    ThemeManager.Current.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                left += pinWidth;
            }

            Rectangle textRect = new Rectangle(
                left,
                WindowChrome.Scale(this, 2),
                Math.Max(0, Width - left - rightPadding),
                Height - WindowChrome.Scale(this, 3));

            TextRenderer.DrawText(
                e.Graphics,
                _titleText ?? string.Empty,
                Font,
                textRect,
                _titleColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            DrawFades(e.Graphics);
        }

        /// <summary>
        /// 标签滚到视口边缘时，用背景色渐变盖住被裁切的一端。
        /// 渐变画在标签自身上，避免额外覆盖窗口拦截鼠标事件。
        /// </summary>
        private void DrawFades(Graphics graphics)
        {
            if (_fadeLeft <= 0 && _fadeRight <= 0)
            {
                return;
            }

            Color background = WindowChrome.BackdropColor;

            if (_fadeLeft > 0)
            {
                int width = Math.Min(_fadeLeft, Width);
                if (width > 1)
                {
                    Rectangle rect = new Rectangle(0, 0, width, Height);
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, width, Height),
                        background,
                        Color.FromArgb(0, background),
                        LinearGradientMode.Horizontal))
                    {
                        graphics.FillRectangle(brush, rect);
                    }
                }
            }

            if (_fadeRight > 0)
            {
                int width = Math.Min(_fadeRight, Width);
                if (width > 1)
                {
                    int x = Width - width;
                    Rectangle rect = new Rectangle(x, 0, width, Height);
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(x, 0, width, Height),
                        Color.FromArgb(0, background),
                        background,
                        LinearGradientMode.Horizontal))
                    {
                        graphics.FillRectangle(brush, rect);
                    }
                }
            }
        }

        private void UpdateVisual()
        {
            ThemePalette palette = ThemeManager.Current;
            Color inactive = WindowChrome.MicaEnabled ? WindowChrome.BackdropColor : palette.TabInactive;
            Color background = _active
                ? palette.TabActive
                : (_hovered ? palette.TabHover : inactive);

            BackColor = background;
            _closeButton.UseVisualStyleBackColor = false;
            _closeButton.BackColor = background;
            _closeButton.FlatAppearance.MouseOverBackColor = palette.CloseHover;
            _closeButton.FlatAppearance.MouseDownBackColor = palette.CloseHover;
            _audioButton.BackColor = background;
            _audioButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            _audioButton.FlatAppearance.MouseDownBackColor = palette.ButtonHover;
            _indicator.BackColor = _active ? palette.Accent : background;

            if (_tab == null)
            {
                _titleText = "新标签页";
                _titleColor = palette.TextSecondary;
                _closeButton.ForeColor = palette.TextSecondary;
            }
            else
            {
                string prefix = string.Empty;

                if (_tab.IsDiscarded)
                {
                    prefix += "◌ ";
                }

                _titleText = prefix + _tab.DisplayTitle;
                _titleColor = _active
                    ? palette.TextPrimary
                    : (_tab.IsDiscarded ? palette.TextDisabled : palette.TextSecondary);
                _closeButton.ForeColor = _active ? palette.TextPrimary : palette.TextSecondary;
            }

            Invalidate();
        }

        private void OnMouseEnter(object sender, EventArgs e)
        {
            _hovered = true;
            UpdateVisual();
            UpdateCloseVisibility();
        }

        private void OnMouseLeave(object sender, EventArgs e)
        {
            _hovered = false;
            UpdateVisual();
            UpdateCloseVisibility();
        }

        private void OnCloseMouseEnter(object sender, EventArgs e)
        {
            _closeButton.BackColor = ThemeManager.Current.CloseHover;
            _closeButton.ForeColor = Color.White;
            UpdateCloseVisibility();
        }

        private void OnCloseMouseLeave(object sender, EventArgs e)
        {
            UpdateVisual();
            UpdateCloseVisibility();
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 上一次拖拽若没有触发 Click，残留的抑制标记在这里清掉。
                _suppressSelect = false;
                return;
            }

            if (e.Button == MouseButtons.Middle)
            {
                Raise(MiddleCloseRequested);
            }
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 指示条专用：视觉上盖住整条标签（含关闭按钮顶部），但不参与鼠标命中。
        /// </summary>
        private sealed class ClickThroughPanel : Panel
        {
            private const int WmNcHitTest = 0x0084;
            private const int HtTransparent = -1;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmNcHitTest)
                {
                    m.Result = (IntPtr)HtTransparent;
                    return;
                }

                base.WndProc(ref m);
            }
        }
    }
}
