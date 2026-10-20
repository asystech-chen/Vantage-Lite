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

            // 添加顺序决定停靠优先级：最后添加的 Top 最靠上。标题栏（标签条）在最上面，地址栏在下。
            Controls.Add(_contentHost);
            Controls.Add(_bookmarksBar);
            Controls.Add(_navigationBar);
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
