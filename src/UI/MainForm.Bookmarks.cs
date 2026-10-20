using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Downloads;
using VantageLite.Storage;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的书签与历史（partial）：Ctrl+D、内置页面、清空历史。
    /// 依赖：BookmarkStore / HistoryStore / InternalPages。改动注意：只在 UI 线程访问存储。
    /// </summary>
    public sealed partial class MainForm
    {
        private const string NewTabAddress = "about:newtab";
        private const string HistoryAddress = "about:history";
        private const string DownloadsAddress = "about:downloads";
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

            string html = BuildInternalPageHtml(address);

            if (html == null)
            {
                html = InternalPages.Bookmarks(_bookmarks, _expandedBookmarks);
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

        /// <summary>
        /// 按 about: 地址生成内置页 HTML；丢弃的标签恢复时也走这里，保证内容与当前设置一致。
        /// </summary>
        private string BuildInternalPageHtml(string address)
        {
            if (string.Equals(address, NewTabAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.NewTab(_settings, _bookmarks);
            }

            if (string.Equals(address, HistoryAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.History(_history.Items);
            }

            if (string.Equals(address, DownloadsAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.Downloads(_downloads.Items);
            }

            if (string.Equals(address, AboutAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.About(Application.ProductVersion, WebViewEnvironment.RuntimeVersion);
            }

            if (string.Equals(address, PreferencesAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.Preferences(_settings);
            }

            if (string.Equals(address, BookmarksAddress, StringComparison.OrdinalIgnoreCase))
            {
                return InternalPages.Bookmarks(_bookmarks, _expandedBookmarks);
            }

            return null;
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

            string html = InternalPages.Bookmarks(_bookmarks, _expandedBookmarks);

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

        /// <summary>
        /// 历史页重绘：清空历史或过滤状态变化后保持地址与内容一致。
        /// </summary>
        private void RenderHistoryPage()
        {
            if (_tabManager == null)
            {
                return;
            }

            string html = InternalPages.History(_history.Items);

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _tabManager.Tabs[i];

                if (tab.IsLive &&
                    string.Equals(tab.Url, HistoryAddress, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ShowInternalPage(HistoryAddress, html);
                }
            }
        }

        /// <summary>
        /// 下载页重绘：下载状态变化后让 about:downloads 与数据保持一致。
        /// </summary>
        private void RenderDownloadsPage()
        {
            if (_tabManager == null || _downloads == null)
            {
                return;
            }

            string html = InternalPages.Downloads(_downloads.Items);

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _tabManager.Tabs[i];

                if (tab.IsLive &&
                    string.Equals(tab.Url, DownloadsAddress, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ShowInternalPage(DownloadsAddress, html);
                }
            }
        }

        /// <summary>
        /// 下载页轮询响应：只回传状态文本和百分比，页面原地更新，不重载页面。
        /// </summary>
        private void PostDownloadProgress()
        {
            BrowserTab page = ActiveTab;

            if (page == null ||
                !string.Equals(page.Url, DownloadsAddress, StringComparison.OrdinalIgnoreCase) ||
                _downloads == null)
            {
                return;
            }

            StringBuilder json = new StringBuilder();
            json.Append("{\"items\":[");

            for (int i = 0; i < _downloads.Items.Count; i++)
            {
                DownloadItem item = _downloads.Items[i];

                if (i > 0)
                {
                    json.Append(',');
                }

                int percent = item.TotalBytes > 0
                    ? (int)Math.Round(item.Progress * 100)
                    : (item.IsCompleted ? 100 : 0);

                json.Append("{\"id\":\"").Append(EscapeJson(item.Id))
                    .Append("\",\"text\":\"").Append(EscapeJson(InternalPages.DescribeDownload(item)))
                    .Append("\",\"percent\":").Append(percent).Append('}');
            }

            json.Append("]}");
            page.PostWebMessage(json.ToString());
        }

        private void HandleDownloadAction(BookmarkCommand command)
        {
            DownloadItem item = null;

            for (int i = 0; i < _downloads.Items.Count; i++)
            {
                if (string.Equals(_downloads.Items[i].Id, command.Id, StringComparison.Ordinal))
                {
                    item = _downloads.Items[i];
                    break;
                }
            }

            if (item == null)
            {
                Log.Warn("[download] action item missing: " + command.Id);
                return;
            }

            string action = (command.Name ?? string.Empty).Trim().ToLowerInvariant();
            Log.Info("[download] action " + action + " id=" + command.Id);

            switch (action)
            {
                case "open":
                    _downloads.OpenFile(item);
                    break;
                case "folder":
                    _downloads.OpenFolder(item);
                    break;
                case "delete":
                    _downloads.Delete(item);
                    break;
                case "pause":
                    if (item.Paused)
                    {
                        _downloads.Resume(item);
                    }
                    else
                    {
                        _downloads.Pause(item);
                    }
                    break;
                case "cancel":
                    _downloads.Cancel(item);
                    break;
                case "retry":
                    RetryDownload(item);
                    break;
            }
        }

        /// <summary>
        /// 重试：用新标签重新访问下载地址，让 WebView2 再次触发 DownloadStarting。
        /// </summary>
        private async void RetryDownload(DownloadItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Url) || _tabManager == null)
            {
                return;
            }

            try
            {
                // 复用原记录：先把任务标记为“正在重试”，隐藏工作标签触发下载后接管它。
                _downloads.QueueRetry(item);

                bool started = await _tabManager.NavigateDownloadWorkerAsync(item.Url);

                if (!started)
                {
                    _downloads.FailRetry(item, "无法启动下载");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Retry download failed: " + item.Url, ex);
            }
        }

        private async void TabManager_WebMessage(object sender, string message)
        {
            if (message != null && message.IndexOf("dismissMenus", StringComparison.Ordinal) >= 0)
            {
                MenuPositioner.CloseOpenMenus();
                return;
            }

            BookmarkCommand command = JsonFile.Parse<BookmarkCommand>(message);
            if (command == null || string.IsNullOrEmpty(command.Cmd))
            {
                Log.Warn("[bookmarks] unparsed message: " + message);
                return;
            }

            // 下载页每 700ms 轮询一次进度，不能进日志，否则日志会被刷爆。
            if (string.Equals(command.Cmd, "downloadProgress", StringComparison.OrdinalIgnoreCase))
            {
                PostDownloadProgress();
                return;
            }

            Log.Info("[bookmarks] message: " + message);

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

            if (string.Equals(command.Cmd, "downloadAction", StringComparison.OrdinalIgnoreCase))
            {
                HandleDownloadAction(command);
                return;
            }

            if (string.Equals(command.Cmd, "settings", StringComparison.OrdinalIgnoreCase))
            {
                ApplySettings(command);
                return;
            }

            if (string.Equals(command.Cmd, "pickDownloadDir", StringComparison.OrdinalIgnoreCase))
            {
                PickDownloadDirectory();
                return;
            }

            if (string.Equals(command.Cmd, "theme", StringComparison.OrdinalIgnoreCase))
            {
                ApplyThemeMode(command.ThemeMode);
                return;
            }

            if (string.Equals(command.Cmd, "searchEngine", StringComparison.OrdinalIgnoreCase))
            {
                ApplySearchEngine(command.SearchEngine);
                return;
            }

            if (string.Equals(command.Cmd, "clearCookies", StringComparison.OrdinalIgnoreCase))
            {
                ClearCookies();
                return;
            }

            if (string.Equals(command.Cmd, "clearAllData", StringComparison.OrdinalIgnoreCase))
            {
                await ClearAllUsageDataAsync();
                return;
            }

            if (string.Equals(command.Cmd, "navigate", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(command.Address))
                {
                    NavigateTo(command.Address);
                }

                return;
            }

            if (string.Equals(command.Cmd, "openInternal", StringComparison.OrdinalIgnoreCase))
            {
                string address = NormalizeInternalPage(command.Address);
                if (address != null)
                {
                    OpenInternalPage(address);
                }

                return;
            }

            if (string.Equals(command.Cmd, "clearHistory", StringComparison.OrdinalIgnoreCase))
            {
                _history.Clear();
                Log.Info("History cleared from about:history");
                RenderHistoryPage();
                PostStatus("已清空浏览记录");
                return;
            }

            // 记录书签页文件夹的展开状态，重绘后原样恢复。
            if (string.Equals(command.Cmd, "expanded", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(command.Id))
                {
                    if (command.Open)
                    {
                        _expandedBookmarks.Add(command.Id);
                    }
                    else
                    {
                        _expandedBookmarks.Remove(command.Id);
                    }
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
                    changed = _bookmarks.Move(command.Id, command.ParentId, command.Before);
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

            if (string.Equals(command.Cmd, "delete", StringComparison.OrdinalIgnoreCase))
            {
                _expandedBookmarks.Remove(command.Id);
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

            if (command.SuspendSeconds.HasValue)
            {
                _settings.SuspendAfterSeconds = Math.Max(0, Math.Min(3600, command.SuspendSeconds.Value));
            }

            if (command.DiscardMinutes.HasValue)
            {
                _settings.DiscardAfterMinutes = Math.Max(0, Math.Min(1440, command.DiscardMinutes.Value));
            }

            if (!string.IsNullOrWhiteSpace(command.SearchEngine))
            {
                string engine = command.SearchEngine.Trim();

                _settings.SearchEngine = engine.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)
                    ? "custom:" + engine.Substring(7).Trim()
                    : engine.ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(command.NewTabMode))
            {
                _settings.NewTabMode = command.NewTabMode.Trim().ToLowerInvariant();
            }

            if (command.NewTabUrl != null)
            {
                _settings.NewTabUrl = command.NewTabUrl.Trim();
            }

            if (command.NewTabShortcuts.HasValue)
            {
                _settings.NewTabShortcuts = command.NewTabShortcuts.Value;
            }

            if (!string.IsNullOrWhiteSpace(command.ThemeMode))
            {
                _settings.ThemeMode = command.ThemeMode.Trim().ToLowerInvariant();
            }

            if (command.ClearOnExit.HasValue)
            {
                _settings.ClearOnExit = command.ClearOnExit.Value;
            }

            if (command.RestoreSession.HasValue)
            {
                _settings.RestoreSession = command.RestoreSession.Value;
            }

            // 隐私优先：开启关闭时清空后不允许恢复会话。
            if (_settings.ClearOnExit)
            {
                _settings.RestoreSession = false;
            }

            if (!string.IsNullOrWhiteSpace(command.HomeMode))
            {
                _settings.HomeMode = command.HomeMode.Trim().ToLowerInvariant();
            }

            if (command.CustomSearchEngines != null)
            {
                List<CustomSearchEngine> engines = new List<CustomSearchEngine>();

                for (int i = 0; i < command.CustomSearchEngines.Count; i++)
                {
                    CustomSearchEngine engine = command.CustomSearchEngines[i];

                    if (engine == null ||
                        string.IsNullOrWhiteSpace(engine.Name) ||
                        string.IsNullOrWhiteSpace(engine.Url))
                    {
                        continue;
                    }

                    string url = engine.Url.Trim();

                    if (url.IndexOf("://", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    engines.Add(new CustomSearchEngine
                    {
                        Name = engine.Name.Trim(),
                        Url = url
                    });
                }

                _settings.CustomSearchEngines = engines;
            }

            if (command.DownloadDirectory != null)
            {
                _settings.DownloadDirectory = command.DownloadDirectory.Trim();
            }

            if (_settings.SearchEngine != null &&
                _settings.SearchEngine.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                bool found = false;

                if (_settings.CustomSearchEngines != null)
                {
                    for (int i = 0; i < _settings.CustomSearchEngines.Count; i++)
                    {
                        CustomSearchEngine item = _settings.CustomSearchEngines[i];

                        if (item != null &&
                            string.Equals("custom:" + item.Name, _settings.SearchEngine, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    _settings.SearchEngine = AppSettings.DefaultSearchEngine;
                }
            }

            _settings.ShowBookmarksBar = command.BookmarksBar;

            JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);

            if (_tabManager != null)
            {
                _tabManager.SetMaxLiveTabs(_settings.MaxLiveTabs);
                _tabManager.SetSuspendAfterSeconds(_settings.SuspendAfterSeconds);
                _tabManager.SetDiscardAfterMinutes(_settings.DiscardAfterMinutes);
                _tabManager.SetDownloadDirectory(_settings.DownloadDirectory);
            }

            VantageLite.Theming.ThemeManager.SetMode(_settings.ThemeMode);
            SyncSearchEngineToPages(_settings.SearchEngine);
            RefreshNewTabPages();

            _bookmarksBar.Visible = _settings.ShowBookmarksBar;

            BrowserTab page = ActiveTab;
            if (page != null)
            {
                page.PostWebMessage("已保存");
            }
        }

        /// <summary>
        /// 设置页「浏览…」：选择下载目录，保存后回传给页面更新输入框。
        /// </summary>
        private void PickDownloadDirectory()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择下载文件的保存目录";
                dialog.ShowNewFolderButton = true;

                string current = _settings == null ? string.Empty : _settings.DownloadDirectory;
                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                {
                    dialog.SelectedPath = current;
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _settings.DownloadDirectory = dialog.SelectedPath;
                JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);

                if (_tabManager != null)
                {
                    _tabManager.SetDownloadDirectory(_settings.DownloadDirectory);
                }

                BrowserTab page = ActiveTab;
                if (page != null)
                {
                    page.PostWebMessage(
                        "{\"downloadDir\":\"" + EscapeJson(_settings.DownloadDirectory) + "\"}");
                }
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void ApplyThemeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return;
            }

            string value = mode.Trim().ToLowerInvariant();

            if (value != "system" && value != "light" && value != "dark")
            {
                return;
            }

            _settings.ThemeMode = value;
            JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);
            VantageLite.Theming.ThemeManager.SetMode(value);
            PostStatus("主题已切换");
        }

        private void ApplySearchEngine(string engine)
        {
            if (string.IsNullOrWhiteSpace(engine))
            {
                return;
            }

            string value = engine.Trim();
            string lower = value.ToLowerInvariant();

            if (lower.StartsWith("custom:", StringComparison.Ordinal))
            {
                string name = value.Substring(7).Trim();
                CustomSearchEngine match = null;

                if (_settings.CustomSearchEngines != null)
                {
                    for (int i = 0; i < _settings.CustomSearchEngines.Count; i++)
                    {
                        CustomSearchEngine item = _settings.CustomSearchEngines[i];

                        if (item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            match = item;
                            break;
                        }
                    }
                }

                if (match == null)
                {
                    return;
                }

                value = "custom:" + match.Name;
            }
            else
            {
                if (lower != "bing" && lower != "google" && lower != "baidu" && lower != "duckduckgo")
                {
                    return;
                }

                value = lower;
            }

            _settings.SearchEngine = value;
            JsonFile.Save(Path.Combine(AppPaths.DataDirectory, "settings.json"), _settings);
            SyncSearchEngineToPages(value);
        }

        /// <summary>
        /// 设置变化后重绘已打开的新标签页，让自定义搜索引擎立即出现。
        /// </summary>
        private void RefreshNewTabPages()
        {
            if (_tabManager == null)
            {
                return;
            }

            string html = InternalPages.NewTab(_settings, _bookmarks);

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _tabManager.Tabs[i];

                if (tab.IsLive &&
                    string.Equals(tab.Url, NewTabAddress, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ShowInternalPage(NewTabAddress, html);
                }
            }
        }

        /// <summary>
        /// 搜索引擎变化后，同步已打开的新标签页与设置页里的下拉框。
        /// </summary>
        private void SyncSearchEngineToPages(string engine)
        {
            if (_tabManager == null || string.IsNullOrEmpty(engine))
            {
                return;
            }

            string safe = engine.Replace("\\", "\\\\").Replace("'", "\\'");
            string script = "var e=document.getElementById('ntEngine')||document.getElementById('search');"
                + "if(e){e.value='" + safe + "';}";

            for (int i = 0; i < _tabManager.Tabs.Count; i++)
            {
                BrowserTab tab = _tabManager.Tabs[i];
                if (!tab.IsLive)
                {
                    continue;
                }

                string url = tab.Url;
                if (string.Equals(url, NewTabAddress, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(url, PreferencesAddress, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ExecuteScript(script);
                }
            }
        }

        private void ClearCookies()
        {
            CoreWebView2Profile profile = _tabManager == null ? null : _tabManager.GetProfile();

            if (profile == null)
            {
                PostStatus("当前没有可用标签，Cookie 未清理");
                return;
            }

            try
            {
                profile.CookieManager.DeleteAllCookies();
                Log.Info("All cookies cleared");
                PostStatus("已清理所有 Cookie");
            }
            catch (Exception ex)
            {
                Log.Error("Clear cookies failed", ex);
                PostStatus("清理 Cookie 失败");
            }
        }

        private async Task ClearAllUsageDataAsync()
        {
            _history.Clear();
            RenderHistoryPage();

            CoreWebView2Profile profile = _tabManager == null ? null : _tabManager.GetProfile();

            if (profile != null)
            {
                try
                {
                    await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
                    Log.Info("All browsing data cleared");
                }
                catch (Exception ex)
                {
                    Log.Error("Clear browsing data failed", ex);
                }
            }

            PostStatus("已清空所有使用痕迹");
        }

        private void PostStatus(string text)
        {
            BrowserTab page = ActiveTab;

            if (page != null)
            {
                page.PostWebMessage(text);
            }
        }

        private void ShowAboutDialog()
        {
            if (AboutDialog.Show(this, Application.ProductVersion))
            {
                ShowDiagnostics();
            }
        }

        private void ShowDiagnostics()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Vantage Lite " + Application.ProductVersion);
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            sb.AppendLine("OS: " + Environment.OSVersion.VersionString
                + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")");
            sb.AppendLine("CLR: " + Environment.Version);
            sb.AppendLine("Process: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            sb.AppendLine("Exe: " + Application.ExecutablePath);
            sb.AppendLine("WebView2: " + (WebViewEnvironment.RuntimeVersion ?? "未检测到"));

            string userData = AppPaths.ResolveUserDataDir(_options.UserDataDir);
            string logDir = string.IsNullOrWhiteSpace(_options.LogFile)
                ? AppPaths.DefaultLogDir
                : Path.GetDirectoryName(Path.GetFullPath(_options.LogFile)) ?? AppPaths.DefaultLogDir;

            sb.AppendLine("Data: " + AppPaths.DataDirectory);
            sb.AppendLine("UserData: " + userData);
            sb.AppendLine("Logs: " + logDir);
            sb.AppendLine();
            sb.AppendLine("Memory: " + DiagnosticsInfo.GetMemorySummary());
            sb.AppendLine();

            int total = 0;
            int live = 0;
            int suspended = 0;
            int discarded = 0;

            if (_tabManager != null)
            {
                for (int i = 0; i < _tabManager.Tabs.Count; i++)
                {
                    BrowserTab tab = _tabManager.Tabs[i];
                    total++;

                    if (tab.IsLive)
                    {
                        live++;
                    }

                    if (tab.IsSuspended)
                    {
                        suspended++;
                    }

                    if (tab.IsDiscarded)
                    {
                        discarded++;
                    }
                }
            }

            sb.AppendLine("Tabs: total=" + total + ", live=" + live
                + ", suspended=" + suspended + ", discarded=" + discarded);
            sb.AppendLine("Active: " + (ActiveTab == null ? "none" : ActiveTab.Url));
            sb.AppendLine();

            if (_settings != null)
            {
                sb.AppendLine("Settings:");
                sb.AppendLine("  theme=" + _settings.ThemeMode
                    + ", search=" + _settings.SearchEngine
                    + ", newTab=" + _settings.NewTabMode
                    + ", home=" + _settings.HomeMode);
                sb.AppendLine("  maxTabs=" + _settings.MaxLiveTabs
                    + ", suspend=" + _settings.SuspendAfterSeconds
                    + ", discard=" + _settings.DiscardAfterMinutes
                    + ", clearOnExit=" + _settings.ClearOnExit);
                sb.AppendLine();
            }

            sb.AppendLine("Args: " + string.Join(" ", Environment.GetCommandLineArgs()));

            DiagnosticsForm.Show(this, sb.ToString(), AppPaths.DataDirectory, logDir);
        }

        private static string NormalizeInternalPage(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            string value = address.Trim().ToLowerInvariant();

            if (value == NewTabAddress || value == "vantage:newtab")
            {
                return NewTabAddress;
            }

            if (value == HistoryAddress || value == "vantage:history")
            {
                return HistoryAddress;
            }

            if (value == DownloadsAddress || value == "vantage:downloads")
            {
                return DownloadsAddress;
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
            DialogResult result = MessageDialog.Show(
                this,
                "清空全部浏览历史？",
                "Vantage Lite",
                MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _history.Clear();
            Log.Info("History cleared");
            RenderHistoryPage();
        }
    
        private void ShowMainMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = VantageLite.Theming.ThemeFonts.Ui(9.5F);
            VantageLite.Theming.ThemeManager.ApplyMenuTheme(menu);

            menu.Items.Add(AddMenuItem("新建标签页", delegate { CreateNewTab(); }));
            menu.Items.Add(AddMenuItem("历史", delegate { OpenInternalPage(HistoryAddress); }));
            menu.Items.Add(AddMenuItem("下载内容", delegate { ToggleDownloadsPanel(); }));
            menu.Items.Add(AddMenuItem("书签管理器", delegate { ShowBookmarkManager(); }));
            menu.Items.Add(AddMenuItem(
                _bookmarksBar.Visible ? "隐藏书签栏" : "显示书签栏",
                delegate { ToggleBookmarksBar(); }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AddMenuItem("设置", delegate { OpenInternalPage(PreferencesAddress); }));
            menu.Items.Add(AddMenuItem("诊断信息", delegate { ShowDiagnostics(); }));
            menu.Items.Add(AddMenuItem("关于 Vantage Lite", delegate { ShowAboutDialog(); }));
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

            MenuPositioner.Show(menu, this, Cursor.Position);
        }

        private static ToolStripMenuItem AddMenuItem(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { action(); };
            return item;
        }
}
}
