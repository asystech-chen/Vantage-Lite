using System;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Diagnostics;

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

        private void ToggleBookmark()
        {
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
            _bookmarksBar.Refresh(_bookmarks.ToolbarFolder);
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
                : InternalPages.Bookmarks(_bookmarks.Root);

            tab.ShowInternalPage(address, html);
            _navigationBar.SetAddress(address);
            UpdateBookmarkState();
        }

        private void ShowBookmarkManager()
        {
            string openUrl = null;

            using (BookmarkManagerForm form = new BookmarkManagerForm(_bookmarks))
            {
                form.OpenRequested += delegate (object sender, string url) { openUrl = url; };
                form.ShowDialog(this);
            }

            _bookmarksBar.Refresh(_bookmarks.ToolbarFolder);
            UpdateBookmarkState();

            if (!string.IsNullOrEmpty(openUrl))
            {
                NavigateTo(openUrl);
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
