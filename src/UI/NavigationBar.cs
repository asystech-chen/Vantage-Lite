using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 顶部导航条：后退/前进/刷新/主页 + 地址栏。
    /// 依赖：ThemeManager / IconFont。改动注意：颜色全部取自主题，不硬编码。
    /// </summary>
    public sealed class NavigationBar : UserControl
    {
        public event EventHandler<string> NavigateRequested;
        public event EventHandler BackRequested;
        public event EventHandler ForwardRequested;
        public event EventHandler ReloadRequested;
        public event EventHandler StopRequested;
        public event EventHandler HomeRequested;
        public event EventHandler BookmarkRequested;

        private readonly ToolTip _toolTip;
        private Button _backButton;
        private Button _forwardButton;
        private Button _reloadButton;
        private Button _homeButton;
        private Button _bookmarkButton;
        private Panel _addressHost;
        private TextBox _addressBox;
        private bool _isLoading;

        public NavigationBar()
        {
            SetStyle(
                ControlStyles.ResizeRedraw |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);

            _toolTip = new ToolTip();
            BuildControls();
            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.ChromeBackground;

            _addressHost.BackColor = palette.AddressBackground;
            _addressBox.BackColor = palette.AddressBackground;
            _addressBox.ForeColor = palette.TextPrimary;

            Button[] buttons = { _backButton, _forwardButton, _reloadButton, _homeButton, _bookmarkButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].BackColor = palette.ChromeBackground;
                buttons[i].ForeColor = palette.TextPrimary;
                buttons[i].FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                buttons[i].FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            }

            _addressHost.Invalidate();
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

        public void SetBookmarked(bool bookmarked)
        {
            _bookmarkButton.Text = bookmarked ? IconFont.StarFilled : IconFont.Star;
            _toolTip.SetToolTip(_bookmarkButton, bookmarked ? "取消收藏 (Ctrl+D)" : "收藏当前页面 (Ctrl+D)");
        }

        public void SetLoading(bool loading)
        {
            _isLoading = loading;
            _reloadButton.Text = loading ? IconFont.Cancel : IconFont.Refresh;
        }

        private void BuildControls()
        {
            Dock = DockStyle.Top;
            Height = 44;
            Padding = new Padding(8, 6, 8, 6);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Left;
            buttons.Width = 144;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Margin = Padding.Empty;
            buttons.Padding = Padding.Empty;

            _backButton = CreateButton(IconFont.Back, "后退 (Alt+←)");
            _forwardButton = CreateButton(IconFont.Forward, "前进 (Alt+→)");
            _reloadButton = CreateButton(IconFont.Refresh, "刷新 (F5)");
            _homeButton = CreateButton(IconFont.Home, "主页");
            _bookmarkButton = CreateButton(IconFont.Star, "收藏当前页面 (Ctrl+D)");

            _backButton.Click += delegate { Raise(BackRequested); };
            _forwardButton.Click += delegate { Raise(ForwardRequested); };
            _homeButton.Click += delegate { Raise(HomeRequested); };
            _bookmarkButton.Click += delegate { Raise(BookmarkRequested); };
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

            FlowLayoutPanel rightButtons = new FlowLayoutPanel();
            rightButtons.Dock = DockStyle.Right;
            rightButtons.Width = 36;
            rightButtons.WrapContents = false;
            rightButtons.FlowDirection = FlowDirection.RightToLeft;
            rightButtons.Margin = Padding.Empty;
            rightButtons.Padding = Padding.Empty;
            rightButtons.Controls.Add(_bookmarkButton);

            _addressBox = new TextBox();
            _addressBox.BorderStyle = BorderStyle.None;
            _addressBox.Font = ThemeFonts.Ui(10.5F);
            _addressBox.Dock = DockStyle.Fill;
            _addressBox.KeyDown += AddressBox_KeyDown;
            _addressBox.GotFocus += delegate { _addressBox.SelectAll(); };

            _addressHost = new BufferedPanel();
            _addressHost.Dock = DockStyle.Fill;
            _addressHost.Padding = new Padding(10, 4, 10, 4);
            _addressHost.Paint += AddressHost_Paint;
            _addressHost.Resize += delegate { _addressHost.Invalidate(); };
            _addressHost.Controls.Add(_addressBox);

            Controls.Add(_addressHost);
            Controls.Add(rightButtons);
            Controls.Add(buttons);

            SetNavigationState(false, false);
        }

        private Button CreateButton(string glyph, string toolTip)
        {
            Button button = new Button();
            button.Text = glyph;
            button.Width = 34;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = IconFont.Create(12F);
            button.TabStop = false;
            button.Margin = new Padding(0, 0, 2, 0);
            button.Cursor = Cursors.Default;
            _toolTip.SetToolTip(button, toolTip);
            return button;
        }

        private void AddressHost_Paint(object sender, PaintEventArgs e)
        {
            if (_addressHost.Width <= 1 || _addressHost.Height <= 1)
            {
                return;
            }

            using (Pen pen = new Pen(ThemeManager.Current.AddressBorder))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, _addressHost.Width - 1, _addressHost.Height - 1);
            }
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

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTheme));
                return;
            }

            ApplyTheme();
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                SetStyle(
                    ControlStyles.ResizeRedraw |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.AllPaintingInWmPaint,
                    true);
            }
        }
    }
}
