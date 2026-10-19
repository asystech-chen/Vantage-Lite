using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Browser;

namespace VantageLite.UI
{
    /// <summary>
    /// 单个标签按钮。由 TabStrip 创建和更新。
    /// 依赖：BrowserTab（只读展示）。改动注意：只负责外观和点击事件。
    /// </summary>
    internal sealed class TabViewItem : Panel
    {
        private static readonly Color TabColor = Color.FromArgb(34, 37, 45);
        private static readonly Color ActiveTabColor = Color.FromArgb(48, 52, 62);
        private static readonly Color AccentColor = Color.FromArgb(64, 156, 255);

        private readonly Label _titleLabel;
        private readonly Button _closeButton;
        private readonly Panel _indicator;

        public TabViewItem(int tabId)
        {
            TabId = tabId;

            Width = 180;
            Height = 28;
            Margin = new Padding(0, 0, 2, 0);
            BackColor = TabColor;
            Cursor = Cursors.Hand;

            _indicator = new Panel();
            _indicator.Dock = DockStyle.Bottom;
            _indicator.Height = 2;
            _indicator.BackColor = TabColor;

            _closeButton = new Button();
            _closeButton.Text = "×";
            _closeButton.Dock = DockStyle.Right;
            _closeButton.Width = 20;
            _closeButton.FlatStyle = FlatStyle.Flat;
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.BackColor = TabColor;
            _closeButton.ForeColor = Color.Gainsboro;
            _closeButton.Font = new Font("Segoe UI", 9F);
            _closeButton.TabStop = false;
            _closeButton.Cursor = Cursors.Hand;
            _closeButton.Click += delegate { Raise(CloseRequested); };
            _closeButton.MouseEnter += delegate { _closeButton.ForeColor = Color.White; };
            _closeButton.MouseLeave += delegate { _closeButton.ForeColor = Color.Gainsboro; };

            _titleLabel = new Label();
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.Padding = new Padding(8, 0, 2, 2);
            _titleLabel.ForeColor = Color.Gainsboro;
            _titleLabel.Font = new Font("Segoe UI", 9F);
            _titleLabel.Cursor = Cursors.Hand;

            Controls.Add(_titleLabel);
            Controls.Add(_closeButton);
            Controls.Add(_indicator);

            Click += delegate { Raise(SelectRequested); };
            _titleLabel.Click += delegate { Raise(SelectRequested); };
            MouseDown += OnMouseDown;
            _titleLabel.MouseDown += OnMouseDown;
        }

        public int TabId { get; private set; }

        public event EventHandler SelectRequested;
        public event EventHandler CloseRequested;

        public void Update(BrowserTab tab, bool active)
        {
            Color background = active ? ActiveTabColor : TabColor;
            BackColor = background;
            _closeButton.BackColor = background;
            _indicator.BackColor = active ? AccentColor : background;

            string prefix = tab.IsDiscarded ? "◌ " : string.Empty;
            _titleLabel.Text = prefix + tab.DisplayTitle;
            _titleLabel.ForeColor = active
                ? Color.White
                : (tab.IsDiscarded ? Color.Gray : Color.Gainsboro);
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
