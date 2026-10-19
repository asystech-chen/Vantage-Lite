using System;
using System.Drawing;
using System.Windows.Forms;

namespace VantageLite.UI
{
    /// <summary>
    /// 顶部导航条：后退/前进/刷新/主页 + 地址栏。
    /// 依赖：无业务依赖，只通过事件与 MainForm 通信。
    /// 改动注意：保持这里是纯 UI，不在控件事件里做导航逻辑。
    /// </summary>
    public sealed class NavigationBar : UserControl
    {
        public event EventHandler<string> NavigateRequested;
        public event EventHandler BackRequested;
        public event EventHandler ForwardRequested;
        public event EventHandler ReloadRequested;
        public event EventHandler StopRequested;
        public event EventHandler HomeRequested;

        private static readonly Color BarColor = Color.FromArgb(30, 33, 40);
        private static readonly Color ButtonHoverColor = Color.FromArgb(58, 63, 76);
        private static readonly Color AddressColor = Color.FromArgb(44, 48, 58);

        private readonly ToolTip _toolTip;
        private Button _backButton;
        private Button _forwardButton;
        private Button _reloadButton;
        private Button _homeButton;
        private TextBox _addressBox;
        private bool _isLoading;

        public NavigationBar()
        {
            _toolTip = new ToolTip();
            BuildControls();
        }

        public void SetAddress(string url)
        {
            if (!_addressBox.Focused)
            {
                _addressBox.Text = url ?? string.Empty;
            }
        }

        public void FocusAddressBar()
        {
            _addressBox.Focus();
            _addressBox.SelectAll();
        }

        public void SetNavigationState(bool canGoBack, bool canGoForward)
        {
            _backButton.Enabled = canGoBack;
            _forwardButton.Enabled = canGoForward;
        }

        public void SetLoading(bool loading)
        {
            _isLoading = loading;
            _reloadButton.Text = loading ? "×" : "⟳";
        }

        private void BuildControls()
        {
            Dock = DockStyle.Top;
            Height = 40;
            BackColor = BarColor;
            Padding = new Padding(6, 6, 8, 6);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Left;
            buttons.Width = 140;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Margin = Padding.Empty;
            buttons.Padding = Padding.Empty;

            _backButton = CreateButton("←", "后退 (Alt+←)");
            _forwardButton = CreateButton("→", "前进 (Alt+→)");
            _reloadButton = CreateButton("⟳", "刷新 (F5)");
            _homeButton = CreateButton("⌂", "主页");

            _backButton.Click += delegate { Raise(BackRequested); };
            _forwardButton.Click += delegate { Raise(ForwardRequested); };
            _homeButton.Click += delegate { Raise(HomeRequested); };
            _reloadButton.Click += delegate
            {
                if (_isLoading)
                {
                    Raise(StopRequested);
                }
                else
                {
                    Raise(ReloadRequested);
                }
            };

            buttons.Controls.Add(_backButton);
            buttons.Controls.Add(_forwardButton);
            buttons.Controls.Add(_reloadButton);
            buttons.Controls.Add(_homeButton);

            _addressBox = new TextBox();
            _addressBox.Dock = DockStyle.Fill;
            _addressBox.BorderStyle = BorderStyle.FixedSingle;
            _addressBox.Font = new Font("Segoe UI", 10F);
            _addressBox.BackColor = AddressColor;
            _addressBox.ForeColor = Color.White;
            _addressBox.KeyDown += AddressBox_KeyDown;
            _addressBox.GotFocus += delegate { _addressBox.SelectAll(); };

            Panel addressHost = new Panel();
            addressHost.Dock = DockStyle.Fill;
            addressHost.Padding = new Padding(6, 2, 0, 2);
            addressHost.Controls.Add(_addressBox);

            Controls.Add(addressHost);
            Controls.Add(buttons);

            SetNavigationState(false, false);
        }

        private Button CreateButton(string text, string toolTip)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 32;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = BarColor;
            button.ForeColor = Color.Gainsboro;
            button.Font = new Font("Segoe UI Symbol", 11F);
            button.TabStop = false;
            button.Margin = new Padding(0, 0, 2, 0);
            button.Cursor = Cursors.Hand;

            button.MouseEnter += delegate { button.BackColor = ButtonHoverColor; };
            button.MouseLeave += delegate { button.BackColor = BarColor; };
            _toolTip.SetToolTip(button, toolTip);

            return button;
        }

        private void AddressBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            EventHandler<string> handler = NavigateRequested;
            if (handler != null)
            {
                handler(this, _addressBox.Text);
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
