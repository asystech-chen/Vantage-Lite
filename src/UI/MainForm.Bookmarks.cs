using System;
using System.Windows.Forms;
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
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            string html = string.Equals(address, HistoryAddress, StringComparison.OrdinalIgnoreCase)
                ? InternalPages.History(_history.Items)
                : InternalPages.Bookmarks(_bookmarks);

            tab.ShowInternalPage(address, html);
            _navigationBar.SetAddress(address);
            UpdateBookmarkState();
        }

        private void ShowBookmarkManager()
        {
            ShowInternalPage(BookmarksAddress);
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
        }

        /// <summary>
        /// 书签页操作完成后原位重绘，保持地址栏仍是 about:bookmarks。
        /// </summary>
        private void RenderBookmarksPage()
        {
            BrowserTab tab = ActiveTab;
            if (tab != null && string.Equals(tab.Url, BookmarksAddress, StringComparison.OrdinalIgnoreCase))
            {
                ShowInternalPage(BookmarksAddress);
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
    }
}
