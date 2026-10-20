using System;
using System.IO;
using System.Windows.Forms;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Storage;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的书签与历史（partial）：Ctrl+D、内置页面、清空历史。
    /// 依赖：BookmarkStore / HistoryStore / InternalPages。改动注意：只在 UI 线程访问存储。
    /// </summary>
    public sealed partial class MainForm
    {
        private const string HistoryAddress = "about:history";
        private const string BookmarksAddress = "about:bookmarks";
        private const string AboutAddress = "about:about";
        private const string PreferencesAddress = "about:preferences";

        // 消息过滤器和页面脚本可能同时触发同一个快捷键，做一次短去抖。
        private DateTime _lastBookmarkToggleUtc;
        private DateTime _lastBarToggleUtc;

        private void ToggleBookmark()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastBookmarkToggleUtc).TotalMilliseconds < 300)
            {
                return;
            }

            _lastBookmarkToggleUtc = now;

            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            string url = tab.Url;
            if (string.IsNullOrWhiteSpace(url) ||
                url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool added = _bookmarks.Toggle(tab.DisplayTitle, url);
            Log.Info((added ? "Bookmark added: " : "Bookmark removed: ") + url);
            _bookmarksBar.Refresh(_bookmarks);
            UpdateBookmarkState();
        }

        private void UpdateBookmarkState()
        {
            BrowserTab tab = ActiveTab;
            _navigationBar.SetBookmarked(tab != null && _bookmarks.Contains(tab.Url));
        }

        private void ShowInternalPage(string address)
        {
            RenderInternalPage(address, true);
        }

        private void RenderInternalPage(string address, bool scheduleFaviconRefresh)
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            string html;

            if (string.Equals(address, HistoryAddress, StringComparison.OrdinalIgnoreCase))
            {
                html = InternalPages.History(_history.Items);
            }
            else if (string.Equals(address, AboutAddress, StringComparison.OrdinalIgnoreCase))
            {
                html = InternalPages.About(Application.ProductVersion, WebViewEnvironment.RuntimeVersion);
            }
            else if (string.Equals(address, PreferencesAddress, StringComparison.OrdinalIgnoreCase))
            {
                html = InternalPages.Preferences(_settings);
            }
            else
            {
                html = InternalPages.Bookmarks(_bookmarks);
            }

            tab.ShowInternalPage(address, html);
            _navigationBar.SetAddress(address);
            UpdateBookmarkState();

            if (scheduleFaviconRefresh)
            {
                _faviconRefreshAddress = address;
                _faviconRefreshVersion = FaviconCache.Version;
                _faviconRefreshTimer.Stop();
                _faviconRefreshTimer.Start();
            }
        }

        private void FaviconRefreshTimer_Tick(object sender, EventArgs e)
        {
            _faviconRefreshTimer.Stop();

            if (_faviconRefreshVersion == FaviconCache.Version)
            {
                return;
            }

            BrowserTab tab = ActiveTab;
            if (tab == null ||
                !string.Equals(tab.Url, _faviconRefreshAddress, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 图标异步加载完成后重绘一次，让内联 data URI 出现在页面上。
            RenderInternalPage(_faviconRefreshAddress, false);
        }

        private void ShowBookmarkManager()
        {
            OpenInternalPage(BookmarksAddress);
        }

        /// <summary>
        /// 内置页面统一入口：已存在相同页面就切过去并刷新，否则新建标签。
        /// </summary>
        private async void OpenInternalPage(string address)
        {
            if (_tabManager == null)
            {
                return;
            }

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab existing = _tabManager.Tabs[i];

                if (string.Equals(existing.Url, address, StringComparison.OrdinalIgnoreCase))
                {
                    await _tabManager.ActivateTabAsync(existing.Id);
                    RenderInternalPage(address, true);
                    return;
                }
            }

            try
            {
                await _tabManager.CreateTabAsync("about:blank");
                RenderInternalPage(address, true);
            }
            catch (Exception ex)
            {
                Log.Error("Open internal page failed: " + address, ex);
            }
        }

        private async void OpenBookmarkInNewTab(string url)
        {
            if (_tabManager == null || string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                await _tabManager.CreateTabAsync(url);
            }
            catch (Exception ex)
            {
                Log.Error("Open bookmark failed: " + url, ex);
            }
        }

        private void ToggleBookmarksBar()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastBarToggleUtc).TotalMilliseconds < 300)
            {
                return;
            }

            _lastBarToggleUtc = now;
            _bookmarksBar.Visible = !_bookmarksBar.Visible;
            _settings.ShowBookmarksBar = _bookmarksBar.Visible;
            JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);
        }

        /// <summary>
        /// 书签页操作完成后原位重绘，保持地址栏仍是 about:bookmarks。
        /// </summary>
        private void RenderBookmarksPage()
        {
            if (_tabManager == null)
            {
                return;
            }

            string html = InternalPages.Bookmarks(_bookmarks);

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _tabManager.Tabs[i];

                if (tab.IsLive &&
                    string.Equals(tab.Url, BookmarksAddress, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ShowInternalPage(BookmarksAddress, html);
                }
            }
        }

        private void TabManager_WebMessage(object sender, string message)
        {
            Log.Info("[bookmarks] message: " + message);

            BookmarkCommand command = JsonFile.Parse<BookmarkCommand>(message);
            if (command == null || string.IsNullOrEmpty(command.Cmd))
            {
                Log.Warn("[bookmarks] unparsed message: " + message);
                return;
            }

            if (string.Equals(command.Cmd, "ping", StringComparison.OrdinalIgnoreCase))
            {
                Log.Info("[bookmarks] page ready");

                BrowserTab page = ActiveTab;
                if (page != null)
                {
                    page.PostWebMessage("已连接");
                }

                return;
            }

            if (string.Equals(command.Cmd, "shortcut", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(command.Name, "bookmark", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleBookmark();
                }
                else if (string.Equals(command.Name, "bookmarksbar", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleBookmarksBar();
                }
                else if (string.Equals(command.Name, "bookmarks", StringComparison.OrdinalIgnoreCase))
                {
                    ShowBookmarkManager();
                }

                return;
            }

            if (string.Equals(command.Cmd, "settings", StringComparison.OrdinalIgnoreCase))
            {
                ApplySettings(command);
                return;
            }

            bool changed;

            switch (command.Cmd)
            {
                case "newFolder":
                    changed = _bookmarks.AddFolder(command.ParentId, command.Name) != null;
                    break;
                case "rename":
                    changed = _bookmarks.Rename(command.Id, command.Name);
                    break;
                case "delete":
                    changed = _bookmarks.Remove(command.Id);
                    break;
                case "cut":
                    changed = _bookmarks.Cut(command.Id);
                    break;
                case "copy":
                    changed = _bookmarks.CopyToClipboard(command.Id);
                    break;
                case "paste":
                    changed = _bookmarks.Paste(command.ParentId);
                    break;
                case "move":
                    changed = _bookmarks.Move(command.Id, command.ParentId);
                    break;
                default:
                    changed = false;
                    break;
            }

            Log.Info("[bookmarks] " + command.Cmd + " changed=" + changed + " id=" + command.Id + " parent=" + command.ParentId);

            if (!changed)
            {
                BrowserTab page = ActiveTab;
                if (page != null)
                {
                    page.PostWebMessage("操作未完成：" + command.Cmd);
                }

                return;
            }

            _bookmarksBar.Refresh(_bookmarks);
            UpdateBookmarkState();
            RenderBookmarksPage();
        }

        private void ApplySettings(BookmarkCommand command)
        {
            _settings.HomeUrl = string.IsNullOrWhiteSpace(command.Home)
                ? AppSettings.DefaultHomeUrl
                : command.Home.Trim();
            _settings.MaxLiveTabs = Math.Max(1, Math.Min(50, command.MaxTabs));
            _settings.ShowBookmarksBar = command.BookmarksBar;

            JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);

            if (_tabManager != null)
            {
                _tabManager.SetMaxLiveTabs(_settings.MaxLiveTabs);
            }

            _bookmarksBar.Visible = _settings.ShowBookmarksBar;

            BrowserTab page = ActiveTab;
            if (page != null)
            {
                page.PostWebMessage("已保存");
            }
        }

        private static string NormalizeInternalPage(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string value = address.Trim().ToLowerInvariant();

            if (value == HistoryAddress || value == "vantage:history")
            {
                return HistoryAddress;
            }

            if (value == BookmarksAddress || value == "vantage:bookmarks")
            {
                return BookmarksAddress;
            }

            if (value == AboutAddress || value == "vantage:about")
            {
                return AboutAddress;
            }

            if (value == PreferencesAddress || value == "vantage:preferences")
            {
                return PreferencesAddress;
            }

            return null;
        }

        private void ClearHistory()
        {
            DialogResult result = MessageBox.Show(
                this,
                "清空全部浏览历史？",
                "Vantage Lite",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _history.Clear();
            Log.Info("History cleared");

            BrowserTab tab = ActiveTab;
            if (tab != null && string.Equals(tab.Url, HistoryAddress, StringComparison.OrdinalIgnoreCase))
            {
                ShowInternalPage(HistoryAddress);
            }
        }
    
        private void ShowMainMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = VantageLite.Theming.ThemeFonts.Ui(9.5F);

            menu.Items.Add(AddMenuItem("新建标签页", delegate { CreateNewTab(); }));
            menu.Items.Add(AddMenuItem("历史", delegate { OpenInternalPage(HistoryAddress); }));
            menu.Items.Add(AddMenuItem("书签管理器", delegate { ShowBookmarkManager(); }));
            menu.Items.Add(AddMenuItem(
                _bookmarksBar.Visible ? "隐藏书签栏" : "显示书签栏",
                delegate { ToggleBookmarksBar(); }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AddMenuItem("设置", delegate { OpenInternalPage(PreferencesAddress); }));
            menu.Items.Add(AddMenuItem("关于 Vantage Lite", delegate { OpenInternalPage(AboutAddress); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AddMenuItem("退出", delegate { Close(); }));

            menu.Closed += delegate
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(menu.Dispose));
                        return;
                    }
                    catch
                    {
                    }
                }

                menu.Dispose();
            };

            menu.Show(Cursor.Position);
        }

        private static ToolStripMenuItem AddMenuItem(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { action(); };
            return item;
        }
}
}
