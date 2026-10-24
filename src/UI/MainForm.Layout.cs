using System;
using System.Drawing;
using System.Windows.Forms;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的界面搭建部分：只放控件创建、布局、导航条事件接线。
    /// 依赖：NavigationBar / TabStrip。改动注意：业务逻辑留在 MainForm.cs。
    /// </summary>
    public sealed partial class MainForm
    {
        private void BuildLayout()
        {
            SuspendLayout();

            Text = "Vantage Lite";
            Icon = LoadApplicationIcon();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 800);
            MinimumSize = new Size(800, 560);
            // 顶部不留缩放边：标签要贴住窗口上缘，方便鼠标直接命中。
            Padding = new Padding(WindowChrome.ResizeBorder, 0, WindowChrome.ResizeBorder, WindowChrome.ResizeBorder);
            BackColor = VantageLite.Theming.ThemeManager.Current.WindowBackground;

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;

            _titleBar = new TitleBar();
            _titleBar.Dock = DockStyle.Top;

            _tabStrip = _titleBar.Tabs;

            _navigationBar = new NavigationBar();
            _navigationBar.Dock = DockStyle.Top;

            _bookmarksBar = new BookmarksBar();
            _bookmarksBar.Dock = DockStyle.Top;

            _privacyBanner = new Panel();
            _privacyBanner.Dock = DockStyle.Top;
            _privacyBanner.Height = 30;
            _privacyBanner.Visible = false;
            _privacyBanner.Paint += PrivacyBanner_Paint;

            _privacyLabel = new Label();
            _privacyLabel.Visible = false;
            _privacyLabel.Dock = DockStyle.Fill;
            _privacyLabel.TextAlign = ContentAlignment.MiddleLeft;
            _privacyLabel.Padding = new Padding(32, 0, 8, 0);
            _privacyLabel.BackColor = Color.Transparent;
            _privacyLabel.Font = VantageLite.Theming.ThemeFonts.Ui(9F, FontStyle.Bold);
            _privacyLabel.Text = "隐私浏览模式 · 不保存历史、Cookie、缓存和下载记录";

            _privacyExitButton = new FlatButton();
            _privacyExitButton.Text = "退出并清除";
            _privacyExitButton.Dock = DockStyle.Right;
            _privacyExitButton.Width = 100;
            _privacyExitButton.Font = VantageLite.Theming.ThemeFonts.Ui(8.5F);
            _privacyExitButton.Click += delegate { _ = ExitPrivacyModeAsync(); };

            _privacyBanner.Controls.Add(_privacyLabel);
            _privacyBanner.Controls.Add(_privacyExitButton);

            // 添加顺序决定停靠优先级：最后添加的 Top 最靠上。标题栏（标签条）在最上面，地址栏在下。
            Controls.Add(_contentHost);
            Controls.Add(_bookmarksBar);
            Controls.Add(_navigationBar);
            Controls.Add(_privacyBanner);
            Controls.Add(_titleBar);

            ResumeLayout(true);
        }

        private void WireNavigationEvents()
        {
            _navigationBar.NavigateRequested += delegate (object sender, string address)
            {
                NavigateTo(address, true);
            };
            _navigationBar.BackRequested += delegate { GoBack(); };
            _navigationBar.ForwardRequested += delegate { GoForward(); };
            _navigationBar.ReloadRequested += delegate { Reload(); };
            _navigationBar.StopRequested += delegate { StopLoading(); };
            _navigationBar.HomeRequested += delegate { NavigateTo(ResolveHomeTarget()); };
            _navigationBar.BookmarkRequested += delegate { ToggleBookmark(); };
            _navigationBar.DownloadsRequested += delegate { ToggleDownloadsPanel(); };
            _navigationBar.MenuRequested += delegate { ShowMainMenu(); };
            _navigationBar.ReopenClosedTabRequested += delegate { ReopenClosedTab(); };
            _titleBar.MinimizeRequested += delegate { WindowState = FormWindowState.Minimized; };
            _titleBar.MaximizeToggleRequested += delegate { ToggleMaximize(); };
            _titleBar.CloseRequested += delegate { Close(); };
            _tabStrip.DragRequested += delegate { WindowChrome.BeginDrag(this); };
            _tabStrip.ToggleMaximizeRequested += delegate { ToggleMaximize(); };
            _bookmarksBar.OpenRequested += delegate (object sender, string url) { OpenBookmarkInNewTab(url); };
            _bookmarksBar.ManageRequested += delegate { ShowBookmarkManager(); };
            _bookmarksBar.Changed += delegate { UpdateBookmarkState(); RenderBookmarksPage(); };
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }
}
