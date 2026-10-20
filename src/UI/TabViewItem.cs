using System;
using System.Drawing;
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
        private readonly Panel _indicator;
        private readonly Timer _spinner;

        private BrowserTab _tab;
        private bool _active;
        private bool _hovered;
        private bool _loading;
        private int _spinAngle;
        private string _titleText;
        private Color _titleColor;

        public TabViewItem(int tabId)
        {
            TabId = tabId;

            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);

            Width = 180;
            Height = 30;
            Margin = new Padding(0, 0, 2, 0);
            Font = ThemeFonts.Ui(9F);
            Cursor = Cursors.Default;

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

            _indicator = new Panel();
            _indicator.Dock = DockStyle.Top;
            _indicator.Height = 2;

            _closeButton = new Button();
            _closeButton.Text = IconFont.Close;
            _closeButton.Dock = DockStyle.Right;
            _closeButton.Width = 22;
            _closeButton.FlatStyle = FlatStyle.Flat;
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Font = IconFont.Create(8F);
            _closeButton.TabStop = false;
            _closeButton.Cursor = Cursors.Default;
            _closeButton.Click += delegate { Raise(CloseRequested); };
            _closeButton.MouseEnter += OnCloseMouseEnter;
            _closeButton.MouseLeave += OnCloseMouseLeave;

            Controls.Add(_closeButton);
            Controls.Add(_indicator);

            Click += delegate { Raise(SelectRequested); };
            MouseDown += OnMouseDown;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            ApplyTheme();
        }

        public int TabId { get; private set; }

        public event EventHandler SelectRequested;
        public event EventHandler CloseRequested;
        public event EventHandler MiddleCloseRequested;

        public void Update(BrowserTab tab, bool active)
        {
            if (!ReferenceEquals(_tab, tab))
            {
                if (_tab != null)
                {
                    _tab.FaviconChanged -= Tab_FaviconChanged;
                }

                _tab = tab;

                if (_tab != null)
                {
                    _tab.FaviconChanged += Tab_FaviconChanged;
                    _tab.LoadingChanged += Tab_LoadingChanged;
                }
            }

            _active = active;
            SetLoading(tab != null && tab.IsLoading);
            UpdateVisual();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tab != null)
                {
                    _tab.FaviconChanged -= Tab_FaviconChanged;
                    _tab.LoadingChanged -= Tab_LoadingChanged;
                    _tab = null;
                }

                _spinner.Dispose();
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
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (_closeButton != null)
            {
                _closeButton.Visible = Width >= 88;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int rightPadding = (_closeButton != null && _closeButton.Visible)
                ? _closeButton.Width + 14
                : 10;

            const int iconSize = 16;
            int left = 8;
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
                    e.Graphics.FillEllipse(brush, left + 5, (Height - 6) / 2, 6, 6);
                }
            }

            left += iconSize + 6;

            Rectangle textRect = new Rectangle(
                left,
                2,
                Math.Max(0, Width - left - rightPadding),
                Height - 3);

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
        }

        private void UpdateVisual()
        {
            ThemePalette palette = ThemeManager.Current;
            Color background = _active
                ? palette.TabActive
                : (_hovered ? palette.TabHover : palette.TabInactive);

            BackColor = background;
            _closeButton.UseVisualStyleBackColor = false;
            _closeButton.BackColor = background;
            _indicator.BackColor = _active ? palette.Accent : background;

            if (_tab == null)
            {
                _titleText = "新标签页";
                _titleColor = palette.TextSecondary;
                _closeButton.ForeColor = palette.TextSecondary;
            }
            else
            {
                string prefix = _tab.IsDiscarded ? "◌ " : string.Empty;
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
        }

        private void OnMouseLeave(object sender, EventArgs e)
        {
            _hovered = false;
            UpdateVisual();
        }

        private void OnCloseMouseEnter(object sender, EventArgs e)
        {
            _closeButton.BackColor = ThemeManager.Current.CloseHover;
            _closeButton.ForeColor = Color.White;
        }

        private void OnCloseMouseLeave(object sender, EventArgs e)
        {
            UpdateVisual();
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
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
    }
}
