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
        public event EventHandler DownloadsRequested;
        public event EventHandler MenuRequested;
        public event EventHandler ReopenClosedTabRequested;
        public event EventHandler ZoomResetRequested;
        public event EventHandler<int> ZoomStepRequested;

        private readonly ToolTip _toolTip;
        private GlyphButton _backButton;
        private GlyphButton _forwardButton;
        private GlyphButton _reloadButton;
        private GlyphButton _homeButton;
        private GlyphButton _bookmarkButton;
        private GlyphButton _downloadButton;
        private GlyphButton _restoreButton;
        private GlyphButton _menuButton;
        private Panel _addressHost;
        private TextBox _addressBox;
        private Label _securityIcon;
        private Label _zoomLabel;
        private Label _zoomOutButton;
        private Label _zoomInButton;
        private bool _showZoomControls;
        private double _zoomFactor = 1.0;
        private double _defaultZoomFactor = 1.0;
        private string _currentUrl;
        private bool _settingAddress;
        private bool _addressEdited;
        private FlowLayoutPanel _buttonsPanel;
        private FlowLayoutPanel _rightButtonsPanel;
        private bool _isLoading;
        private Font _addressFont;
        private double _addressFontScale = -1;

        public NavigationBar()
        {
            SetStyle(
                ControlStyles.ResizeRedraw |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);

            _toolTip = new ToolTip();
            ThemeManager.ApplyToolTip(_toolTip);
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

                if (_addressFont != null)
                {
                    _addressFont.Dispose();
                    _addressFont = null;
                }
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

            if (_securityIcon != null)
            {
                _securityIcon.BackColor = palette.AddressBackground;
            }

            if (_zoomLabel != null)
            {
                _zoomLabel.BackColor = palette.AddressBackground;
                _zoomLabel.ForeColor = palette.TextSecondary;
            }

            if (_zoomOutButton != null)
            {
                _zoomOutButton.BackColor = palette.AddressBackground;
                _zoomOutButton.ForeColor = palette.TextSecondary;
            }

            if (_zoomInButton != null)
            {
                _zoomInButton.BackColor = palette.AddressBackground;
                _zoomInButton.ForeColor = palette.TextSecondary;
            }

            UpdateSecurityIcon();

            Button[] buttons = { _backButton, _forwardButton, _reloadButton, _homeButton, _bookmarkButton, _downloadButton, _menuButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].UseVisualStyleBackColor = false;
                buttons[i].BackColor = palette.ChromeBackground;
                buttons[i].ForeColor = buttons[i].Enabled ? palette.TextPrimary : palette.TextDisabled;
                buttons[i].FlatAppearance.MouseOverBackColor =
                    buttons[i].Enabled ? palette.ButtonHover : palette.ChromeBackground;
                buttons[i].FlatAppearance.MouseDownBackColor =
                    buttons[i].Enabled ? palette.ButtonPressed : palette.ChromeBackground;
            }

            _addressHost.Invalidate();
        }

        public void SetAddress(string url)
        {
            _currentUrl = url;

            // 用户正在手工输入时不要覆盖；按回车后允许把补全后的完整 URL 写回来。
            if (!_addressBox.Focused || !_addressEdited)
            {
                _settingAddress = true;
                _addressBox.Text = url ?? string.Empty;
                _settingAddress = false;
            }

            UpdateSecurityIcon();
        }

        public void FocusAddressBar()
        {
            _addressBox.Focus();
            _addressBox.SelectAll();
        }

        /// <summary>
        /// 下载按钮右下角的屏幕坐标，用于把下载面板贴在按钮下方弹出。
        /// </summary>
        public void SetDownloadBadge(bool visible)
        {
            if (_downloadButton != null)
            {
                _downloadButton.Badge = visible;
            }
        }

        public Point GetDownloadsAnchor()
        {
            return _downloadButton.PointToScreen(
                new Point(_downloadButton.Width, _downloadButton.Height));
        }

        public void SetNavigationState(bool canGoBack, bool canGoForward)
        {
            _backButton.Enabled = canGoBack;
            _forwardButton.Enabled = canGoForward;
            ApplyTheme();
        }

        public void SetBookmarked(bool bookmarked)
        {
            _bookmarkButton.Glyph = bookmarked ? IconFont.StarFilled : IconFont.Star;
            _toolTip.SetToolTip(_bookmarkButton, bookmarked ? "取消收藏 (Ctrl+D)" : "收藏当前页面 (Ctrl+D)");
        }

        public void SetLoading(bool loading)
        {
            _isLoading = loading;
            _reloadButton.Glyph = loading ? IconFont.Cancel : IconFont.Refresh;
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
            _buttonsPanel = buttons;

            _backButton = CreateButton(IconFont.Back, "后退 (Alt+←)");
            _forwardButton = CreateButton(IconFont.Forward, "前进 (Alt+→)");
            _reloadButton = CreateButton(IconFont.Refresh, "刷新 (F5)");
            _homeButton = CreateButton(IconFont.Home, "主页");
            _bookmarkButton = CreateButton(IconFont.Star, "收藏当前页面 (Ctrl+D)");
            _downloadButton = CreateButton(IconFont.Download, "下载内容");
            _restoreButton = CreateButton(IconFont.Undo, "重新打开关闭的标签页 (Ctrl+Shift+T)");
            _menuButton = CreateButton(IconFont.Menu, "菜单");

            _backButton.Click += delegate { Raise(BackRequested); };
            _forwardButton.Click += delegate { Raise(ForwardRequested); };
            _homeButton.Click += delegate { Raise(HomeRequested); };
            _bookmarkButton.Click += delegate { Raise(BookmarkRequested); };
            _downloadButton.Click += delegate { Raise(DownloadsRequested); };
            _restoreButton.Click += delegate { Raise(ReopenClosedTabRequested); };
            _menuButton.Click += delegate { Raise(MenuRequested); };
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
            rightButtons.Width = 144;
            rightButtons.WrapContents = false;
            rightButtons.FlowDirection = FlowDirection.RightToLeft;
            rightButtons.Margin = Padding.Empty;
            rightButtons.Padding = Padding.Empty;
            _rightButtonsPanel = rightButtons;
            rightButtons.Controls.Add(_menuButton);
            rightButtons.Controls.Add(_downloadButton);
            rightButtons.Controls.Add(_bookmarkButton);
            rightButtons.Controls.Add(_restoreButton);

            _addressBox = new TextBox();
            _addressBox.BorderStyle = BorderStyle.None;
            _addressBox.Font = ThemeFonts.UiPx(10.5F);
            _addressBox.Dock = DockStyle.None;
            _addressBox.AutoSize = false;
            _addressBox.KeyDown += AddressBox_KeyDown;
            _addressBox.GotFocus += delegate
            {
                _addressEdited = false;
                _addressBox.SelectAll();
            };
            _addressBox.LostFocus += delegate { _addressEdited = false; };
            _addressBox.TextChanged += delegate
            {
                if (!_settingAddress)
                {
                    _addressEdited = true;
                }
            };

            _addressHost = new BufferedPanel();
            _addressHost.Dock = DockStyle.Fill;
            _addressHost.Padding = new Padding(10, 4, 10, 4);
            _addressHost.Paint += AddressHost_Paint;
            _addressHost.Resize += delegate { _addressHost.Invalidate(); };
            _addressHost.Controls.Add(_addressBox);

            _securityIcon = new Label();
            _securityIcon.AutoSize = false;
            _securityIcon.TextAlign = ContentAlignment.MiddleCenter;
            _securityIcon.Font = IconFont.CreatePx(13f);
            _securityIcon.Visible = false;
            _securityIcon.Cursor = Cursors.Default;
            _addressHost.Controls.Add(_securityIcon);

            _zoomLabel = new Label();
            _zoomLabel.AutoSize = false;
            _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            _zoomLabel.Font = ThemeFonts.UiPx(8.5F);
            _zoomLabel.Cursor = Cursors.Hand;
            _zoomLabel.Visible = false;
            _zoomLabel.Click += delegate { Raise(ZoomResetRequested); };
            _zoomLabel.MouseEnter += delegate { SetZoomHover(_zoomLabel, true); };
            _zoomLabel.MouseLeave += delegate { SetZoomHover(_zoomLabel, false); };
            _toolTip.SetToolTip(_zoomLabel, "当前页面缩放；点击恢复默认比例");
            _addressHost.Controls.Add(_zoomLabel);

            _zoomOutButton = new Label();
            _zoomOutButton.AutoSize = false;
            _zoomOutButton.Text = "\u2212";
            _zoomOutButton.TextAlign = ContentAlignment.MiddleCenter;
            _zoomOutButton.Font = ThemeFonts.UiPx(11F);
            _zoomOutButton.Cursor = Cursors.Hand;
            _zoomOutButton.Visible = false;
            _zoomOutButton.Click += delegate { RaiseZoomStep(-1); };
            _zoomOutButton.MouseEnter += delegate { SetZoomHover(_zoomOutButton, true); };
            _zoomOutButton.MouseLeave += delegate { SetZoomHover(_zoomOutButton, false); };
            _toolTip.SetToolTip(_zoomOutButton, "缩小");
            _addressHost.Controls.Add(_zoomOutButton);

            _zoomInButton = new Label();
            _zoomInButton.AutoSize = false;
            _zoomInButton.Text = "+";
            _zoomInButton.TextAlign = ContentAlignment.MiddleCenter;
            _zoomInButton.Font = ThemeFonts.UiPx(11F);
            _zoomInButton.Cursor = Cursors.Hand;
            _zoomInButton.Visible = false;
            _zoomInButton.Click += delegate { RaiseZoomStep(1); };
            _zoomInButton.MouseEnter += delegate { SetZoomHover(_zoomInButton, true); };
            _zoomInButton.MouseLeave += delegate { SetZoomHover(_zoomInButton, false); };
            _toolTip.SetToolTip(_zoomInButton, "放大");
            _addressHost.Controls.Add(_zoomInButton);

            _zoomLabel.BringToFront();
            _zoomOutButton.BringToFront();
            _zoomInButton.BringToFront();
            _addressHost.Resize += delegate
            {
                LayoutAddressBox();
                _addressHost.Invalidate();
            };

            Controls.Add(_addressHost);
            Controls.Add(rightButtons);
            Controls.Add(buttons);

            SetNavigationState(false, false);
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
            ApplyAddressFont();

            int buttonHeight = WindowChrome.Scale(this, 32);
            int textHeight = TextRenderer.MeasureText("国Ag", _addressBox == null ? Font : _addressBox.Font).Height;
            int innerHeight = Math.Max(buttonHeight, textHeight + WindowChrome.Scale(10));

            Height = innerHeight + WindowChrome.Scale(12);
            Padding = new Padding(
                WindowChrome.Scale(this, 8),
                WindowChrome.Scale(this, 6),
                WindowChrome.Scale(this, 8),
                WindowChrome.Scale(this, 6));

            if (_buttonsPanel != null)
            {
                _buttonsPanel.Width = WindowChrome.Scale(this, 144);
            }

            if (_rightButtonsPanel != null)
            {
                _rightButtonsPanel.Width = WindowChrome.Scale(this, 108);
            }

            if (_addressHost != null)
            {
                _addressHost.Padding = new Padding(
                    WindowChrome.Scale(this, 10),
                    WindowChrome.Scale(this, 3),
                    WindowChrome.Scale(this, 10),
                    WindowChrome.Scale(this, 3));
                LayoutAddressBox();
            }

            Button[] navButtons = { _backButton, _forwardButton, _reloadButton, _homeButton, _bookmarkButton, _downloadButton, _menuButton };
            for (int i = 0; i < navButtons.Length; i++)
            {
                if (navButtons[i] == null)
                {
                    continue;
                }

                navButtons[i].Width = WindowChrome.Scale(this, 34);
                navButtons[i].Height = WindowChrome.Scale(this, 32);
                navButtons[i].Margin = new Padding(0, 0, WindowChrome.Scale(this, 2), 0);
            }

            Invalidate();
        }

        private void ApplyAddressFont()
        {
            if (_addressBox == null)
            {
                return;
            }

            double scale = WindowChrome.ScaleFactor;

            if (_addressFont != null && Math.Abs(_addressFontScale - scale) < 0.001)
            {
                return;
            }

            Font old = _addressFont;
            _addressFont = ThemeFonts.UiPx(10.5F);
            _addressFontScale = scale;
            _addressBox.Font = _addressFont;

            if (old != null)
            {
                old.Dispose();
            }
        }

        /// <summary>缩放系数变化时由宿主调用。</summary>
        public void RefreshMetrics()
        {
            ApplyMetrics();
            ApplyTheme();
        }

        /// <summary>
        /// 地址框垂直居中，高度按字体实测，避免高 DPI 下文字被上下裁切。
        /// </summary>
        private void LayoutAddressBox()
        {
            if (_addressBox == null || _addressHost == null)
            {
                return;
            }

            int left = _addressHost.Padding.Left;
            int right = _addressHost.Padding.Right;
            int top = _addressHost.Padding.Top;
            int available = Math.Max(1, _addressHost.ClientSize.Height - top - _addressHost.Padding.Bottom);
            int textHeight = TextRenderer.MeasureText("国Ag", _addressBox.Font).Height;
            int boxHeight = Math.Min(available, textHeight + WindowChrome.Scale(this, 4));
            int y = top + Math.Max(0, (available - boxHeight) / 2);

            if (_securityIcon != null && _securityIcon.Visible)
            {
                int iconWidth = WindowChrome.Scale(this, 18);
                _securityIcon.SetBounds(left, y, iconWidth, boxHeight);
                left += iconWidth + WindowChrome.Scale(this, 4);
            }

            // 缩放控件（− 比例 +）放在地址框右侧；未开启控件时只在非默认比例显示百分比。
            int zoomButton = WindowChrome.Scale(this, 18);
            int zoomText = TextRenderer.MeasureText("1000%", _zoomLabel.Font).Width + WindowChrome.Scale(this, 6);
            int zoomWidth = 0;

            if (_showZoomControls)
            {
                zoomWidth = zoomButton * 2 + zoomText + WindowChrome.Scale(this, 4);
            }
            else if (_zoomLabel.Visible)
            {
                zoomWidth = zoomText;
            }

            if (zoomWidth > 0)
            {
                right += zoomWidth + WindowChrome.Scale(this, 4);
            }

            int width = Math.Max(0, _addressHost.ClientSize.Width - left - right);
            _addressBox.SetBounds(left, y, width, boxHeight);

            if (zoomWidth > 0)
            {
                int zoomX = _addressHost.ClientSize.Width - _addressHost.Padding.Right - zoomWidth;

                if (_showZoomControls)
                {
                    _zoomOutButton.SetBounds(zoomX, y, zoomButton, boxHeight);
                    _zoomLabel.SetBounds(zoomX + zoomButton, y, zoomText, boxHeight);
                    _zoomInButton.SetBounds(zoomX + zoomButton + zoomText, y, zoomButton, boxHeight);
                }
                else
                {
                    _zoomLabel.SetBounds(zoomX, y, zoomText, boxHeight);
                }
            }
        }

        private void RaiseZoomStep(int direction)
        {
            EventHandler<int> handler = ZoomStepRequested;
            if (handler != null)
            {
                handler(this, direction);
            }
        }

        private void SetZoomHover(Label label, bool hover)
        {
            if (label == null)
            {
                return;
            }

            ThemePalette palette = ThemeManager.Current;
            label.BackColor = hover ? palette.ButtonHover : palette.AddressBackground;
            label.ForeColor = hover ? palette.TextPrimary : palette.TextSecondary;
        }

        /// <summary>
        /// 设置缩放控件的展示方式：showControls=true 时始终显示 −/比例/+；
        /// false 时仅在当前比例与默认比例不同时显示百分比。
        /// </summary>
        public void SetZoomMode(bool showControls, double defaultFactor)
        {
            _showZoomControls = showControls;
            _defaultZoomFactor = defaultFactor > 0 ? defaultFactor : 1.0;
            ApplyZoomVisibility();
        }

        /// <summary>
        /// 显示当前页面缩放比例。由宿主在缩放或切换标签时调用。
        /// </summary>
        public void SetZoom(double factor)
        {
            if (_zoomLabel == null)
            {
                return;
            }

            _zoomFactor = factor;
            ApplyZoomVisibility();
        }

        private void ApplyZoomVisibility()
        {
            if (_zoomLabel == null)
            {
                return;
            }

            _zoomLabel.Text = ((int)Math.Round(_zoomFactor * 100)) + "%";
            _zoomLabel.Visible = _showZoomControls || Math.Abs(_zoomFactor - _defaultZoomFactor) > 0.001;
            _zoomOutButton.Visible = _showZoomControls;
            _zoomInButton.Visible = _showZoomControls;
            LayoutAddressBox();
            _addressHost.Invalidate();
        }

        /// <summary>
        /// 地址栏左侧的加密状态：HTTPS 闭合锁，HTTP 打开锁，内部页/空地址不显示。
        /// </summary>
        private void UpdateSecurityIcon()
        {
            if (_securityIcon == null)
            {
                return;
            }

            string scheme = null;
            Uri uri;

            if (!string.IsNullOrEmpty(_currentUrl) &&
                Uri.TryCreate(_currentUrl, UriKind.Absolute, out uri))
            {
                scheme = uri.Scheme;
            }

            if (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                _securityIcon.Text = IconFont.Lock;
                _securityIcon.ForeColor = ThemeManager.Current.TextSecondary;
                _toolTip.SetToolTip(_securityIcon, "连接已加密（HTTPS）");
                _securityIcon.Visible = true;
            }
            else if (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                _securityIcon.Text = IconFont.Unlock;
                _securityIcon.ForeColor = ThemeManager.Current.Warning;
                _toolTip.SetToolTip(_securityIcon, "此页面未加密（HTTP），信息可能被窃听或篡改");
                _securityIcon.Visible = true;
            }
            else
            {
                _securityIcon.Visible = false;
                _toolTip.SetToolTip(_securityIcon, null);
            }

            LayoutAddressBox();
        }

        private GlyphButton CreateButton(string glyph, string toolTip)
        {
            GlyphButton button = new GlyphButton(glyph);
            button.Width = 34;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = IconFont.CreatePx(16f);
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
            _addressEdited = false;

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


    /// <summary>
    /// 用字体图标绘制的按钮：禁用态使用主题的 TextDisabled，避免系统默认的深灰。
    /// 依赖：ThemeManager / IconFont。改动注意：Glyph 变化后需要重新绘制。
    /// </summary>
    internal sealed class GlyphButton : Button
    {
        private string _glyph;
        private bool _hover;
        private bool _pressed;
        private bool _badge;

        public GlyphButton(string glyph)
        {
            _glyph = glyph;
            Text = string.Empty;
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            FlatAppearance.BorderSize = 0;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public string Glyph
        {
            get { return _glyph; }
            set
            {
                _glyph = value;
                Invalidate();
            }
        }

        public bool Badge
        {
            get { return _badge; }
            set
            {
                if (_badge == value)
                {
                    return;
                }

                _badge = value;
                Invalidate();
            }
        }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = false;
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = BackColor;

            if (Enabled)
            {
                if (_pressed)
                {
                    background = FlatAppearance.MouseDownBackColor;
                }
                else if (_hover)
                {
                    background = FlatAppearance.MouseOverBackColor;
                }
            }

            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            Color color = Enabled ? ForeColor : ThemeManager.Current.TextDisabled;

            TextRenderer.DrawText(
                e.Graphics,
                _glyph ?? string.Empty,
                Font,
                ClientRectangle,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);

            if (_badge)
            {
                int size = WindowChrome.Scale(this, 7);
                int x = Math.Max(0, Width - size - WindowChrome.Scale(this, 5));
                int y = WindowChrome.Scale(this, 5);

                using (SolidBrush badge = new SolidBrush(ThemeManager.Current.Accent))
                {
                    e.Graphics.FillEllipse(badge, x, y, size, size);
                }
            }
        }
    }
}
