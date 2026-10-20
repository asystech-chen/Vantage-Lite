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
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 800);
            MinimumSize = new Size(800, 560);
            BackColor = Color.FromArgb(24, 27, 33);

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;

            _tabStrip = new TabStrip();
            _tabStrip.Dock = DockStyle.Top;

            _navigationBar = new NavigationBar();
            _navigationBar.Dock = DockStyle.Top;

            _bookmarksBar = new BookmarksBar();
            _bookmarksBar.Dock = DockStyle.Top;

            // 添加顺序决定停靠优先级：最后添加的 Top 最靠上。标签条在上，地址栏在下。
            Controls.Add(_contentHost);
            Controls.Add(_bookmarksBar);
            Controls.Add(_navigationBar);
            Controls.Add(_tabStrip);

            ResumeLayout(true);
        }

        private void WireNavigationEvents()
        {
            _navigationBar.NavigateRequested += delegate (object sender, string address)
            {
                NavigateTo(address);
            };
            _navigationBar.BackRequested += delegate { GoBack(); };
            _navigationBar.ForwardRequested += delegate { GoForward(); };
            _navigationBar.ReloadRequested += delegate { Reload(); };
            _navigationBar.StopRequested += delegate { StopLoading(); };
            _navigationBar.HomeRequested += delegate { NavigateTo(DefaultHomeUrl); };
            _navigationBar.BookmarkRequested += delegate { ToggleBookmark(); };
            _bookmarksBar.OpenRequested += delegate (object sender, string url) { NavigateTo(url); };
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
