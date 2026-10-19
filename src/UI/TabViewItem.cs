using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 单个标签按钮：标题、关闭按钮、活动指示线。
    /// 依赖：BrowserTab（只读展示）/ ThemeManager。改动注意：只负责外观和点击事件。
    /// </summary>
    internal sealed class TabViewItem : Panel
    {
        private readonly Label _titleLabel;
        private readonly Button _closeButton;
        private readonly Panel _indicator;

        private BrowserTab _tab;
        private bool _active;
        private bool _hovered;

        public TabViewItem(int tabId)
        {
            TabId = tabId;

            Width = 180;
            Height = 30;
            Margin = new Padding(0, 0, 2, 0);
            Cursor = Cursors.Hand;

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
            _closeButton.Cursor = Cursors.Hand;
            _closeButton.Click += delegate { Raise(CloseRequested); };
            _closeButton.MouseEnter += OnCloseMouseEnter;
            _closeButton.MouseLeave += OnCloseMouseLeave;

            _titleLabel = new Label();
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.Padding = new Padding(10, 0, 2, 2);
            _titleLabel.Font = new Font("Segoe UI", 9F);
            _titleLabel.Cursor = Cursors.Hand;

            Controls.Add(_titleLabel);
            Controls.Add(_closeButton);
            Controls.Add(_indicator);

            Click += delegate { Raise(SelectRequested); };
            _titleLabel.Click += delegate { Raise(SelectRequested); };
            MouseDown += OnMouseDown;
            _titleLabel.MouseDown += OnMouseDown;

            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            _titleLabel.MouseEnter += OnMouseEnter;
            _titleLabel.MouseLeave += OnMouseLeave;

            ApplyTheme();
        }

        public int TabId { get; private set; }

        public event EventHandler SelectRequested;
        public event EventHandler CloseRequested;

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
                _titleLabel.Text = "新标签页";
                _titleLabel.ForeColor = palette.TextSecondary;
                _closeButton.ForeColor = palette.TextSecondary;
                return;
            }

            string prefix = _tab.IsDiscarded ? "◌ " : string.Empty;
            _titleLabel.Text = prefix + _tab.DisplayTitle;
            _titleLabel.ForeColor = _active
                ? palette.TextPrimary
                : (_tab.IsDiscarded ? palette.TextDisabled : palette.TextSecondary);
            _closeButton.ForeColor = _active ? palette.TextPrimary : palette.TextSecondary;
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
                Raise(CloseRequested);
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
