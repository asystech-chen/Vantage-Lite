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

        private BrowserTab _tab;
        private bool _active;
        private bool _hovered;
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
            _tab = tab;
            _active = active;
            UpdateVisual();
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
            Rectangle textRect = new Rectangle(
                10,
                2,
                Math.Max(0, Width - rightPadding),
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
