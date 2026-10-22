using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Storage;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的导航与标签操作（partial）。
    /// 依赖：TabManager / UrlHelper。改动注意：所有操作都作用于当前活动标签。
    /// </summary>
    public sealed partial class MainForm
    {
        private const int MaxClosedTabs = 25;

        private string[] _autoFallbacks;
        private bool[] _autoFallbackTried;
        private int _autoFallbackIndex;
        private int _autoFallbackTabId;

        // 本次运行内关闭的标签，供右键菜单 / Ctrl+Shift+T 重新打开。
        private readonly List<ClosedTabInfo> _closedTabs = new List<ClosedTabInfo>();

        private sealed class ClosedTabInfo
        {
            public string Url;
            public string Title;
        }

        private void CreateNewTab()
        {
            if (_tabManager == null)
            {
                return;
            }

            CreateNewTabAsync();
        }

        private async void CreateNewTabAsync()
        {
            try
            {
                await CreateNewTabCoreAsync();
                _navigationBar.FocusAddressBar();
            }
            catch (Exception ex)
            {
                Log.Error("Create tab failed", ex);
            }
        }

        /// <summary>
        /// 直接创建目标标签页，不经过 about:blank：内置页作为首个页面、网址直接加载。
        /// </summary>
        private async Task CreateNewTabCoreAsync()
        {
            if (_tabManager == null || _settings == null)
            {
                return;
            }

            string mode = NormalizeNewTabMode(_settings.NewTabMode);
            string target;

            if (mode == "blank")
            {
                target = "about:blank";
            }
            else if (mode == "home")
            {
                target = ResolveHomeTarget();
            }
            else if (mode == "custom")
            {
                target = NormalizeCustomUrl(_settings.NewTabUrl);

                if (target.Length == 0)
                {
                    target = NewTabAddress;
                }
            }
            else
            {
                target = NewTabAddress;
            }

            await CreateTabForTargetAsync(target);
        }

        private async Task CreateTabForTargetAsync(string target)
        {
            if (string.Equals(target, NewTabAddress, StringComparison.OrdinalIgnoreCase))
            {
                await _tabManager.CreateTabAsync(
                    "about:blank",
                    NewTabAddress,
                    InternalPages.NewTab(_settings, _bookmarks));
                return;
            }

            await _tabManager.CreateTabAsync(target);
        }

        /// <summary>
        /// 按地址新建标签：内部页重建 HTML，普通地址直接导航。
        /// </summary>
        private async Task CreateTabFromAddressAsync(string address)
        {
            if (_tabManager == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                await CreateNewTabCoreAsync();
                return;
            }

            if (string.Equals(address, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                await _tabManager.CreateTabAsync("about:blank");
                return;
            }

            string internalPage = NormalizeInternalPage(address);

            if (internalPage != null)
            {
                await _tabManager.CreateTabAsync(
                    "about:blank",
                    internalPage,
                    BuildInternalPageHtml(internalPage));
                return;
            }

            await _tabManager.CreateTabAsync(address);
        }

        private BrowserTab FindTabById(int id)
        {
            if (_tabManager == null)
            {
                return null;
            }

            IList<BrowserTab> tabs = _tabManager.Tabs;

            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Id == id)
                {
                    return tabs[i];
                }
            }

            return null;
        }

        private int IndexOfTab(int id)
        {
            if (_tabManager == null)
            {
                return -1;
            }

            IList<BrowserTab> tabs = _tabManager.Tabs;

            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 关闭前记录地址与标题；about:blank 不值得恢复，直接跳过。
        /// </summary>
        private void RememberClosedTab(int id)
        {
            BrowserTab tab = FindTabById(id);

            if (tab == null)
            {
                return;
            }

            string url = tab.Url;

            if (string.IsNullOrWhiteSpace(url) ||
                string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _closedTabs.Add(new ClosedTabInfo { Url = url, Title = tab.DisplayTitle });

            if (_closedTabs.Count > MaxClosedTabs)
            {
                _closedTabs.RemoveAt(0);
            }
        }

        private async void DuplicateTab(int id)
        {
            BrowserTab tab = FindTabById(id);

            if (tab == null)
            {
                return;
            }

            try
            {
                await CreateTabFromAddressAsync(tab.Url);
            }
            catch (Exception ex)
            {
                Log.Error("Duplicate tab failed", ex);
            }
        }

        private async void ReopenClosedTab()
        {
            if (_closedTabs.Count == 0)
            {
                return;
            }

            int last = _closedTabs.Count - 1;
            ClosedTabInfo info = _closedTabs[last];
            _closedTabs.RemoveAt(last);

            try
            {
                await CreateTabFromAddressAsync(info.Url);
            }
            catch (Exception ex)
            {
                Log.Error("Reopen tab failed: " + info.Url, ex);
                _closedTabs.Add(info);

                if (_closedTabs.Count > MaxClosedTabs)
                {
                    _closedTabs.RemoveAt(0);
                }
            }
        }

        private async void CloseOtherTabs(int keepId)
        {
            if (_tabManager == null)
            {
                return;
            }

            List<BrowserTab> tabs = new List<BrowserTab>(_tabManager.Tabs);

            try
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    if (tabs[i].Id == keepId)
                    {
                        continue;
                    }

                    RememberClosedTab(tabs[i].Id);
                    await _tabManager.CloseTabAsync(tabs[i].Id);
                }

                await _tabManager.ActivateTabAsync(keepId);
            }
            catch (Exception ex)
            {
                Log.Error("Close other tabs failed", ex);
            }
        }

        private async void CloseTabsToRight(int keepId)
        {
            int index = IndexOfTab(keepId);

            if (_tabManager == null || index < 0)
            {
                return;
            }

            List<BrowserTab> tabs = new List<BrowserTab>(_tabManager.Tabs);

            try
            {
                for (int i = index + 1; i < tabs.Count; i++)
                {
                    RememberClosedTab(tabs[i].Id);
                    await _tabManager.CloseTabAsync(tabs[i].Id);
                }

                await _tabManager.ActivateTabAsync(keepId);
            }
            catch (Exception ex)
            {
                Log.Error("Close tabs to right failed", ex);
            }
        }

        /// <summary>
        /// 启动页目标：ASYS 起始页 / 空白页 / 新标签页 / 自定义页面。
        /// </summary>
        private string ResolveHomeTarget()
        {
            string mode = NormalizeHomeMode(_settings == null ? null : _settings.HomeMode);

            if (mode == "blank")
            {
                return "about:blank";
            }

            if (mode == "newtab")
            {
                return NewTabAddress;
            }

            if (mode == "custom")
            {
                string url = NormalizeCustomUrl(_settings.HomeUrl);

                if (url.Length > 0)
                {
                    return url;
                }
            }

            return AppSettings.DefaultHomeUrl;
        }

        private static string NormalizeHomeMode(string mode)
        {
            if (string.Equals(mode, "blank", StringComparison.OrdinalIgnoreCase))
            {
                return "blank";
            }

            if (string.Equals(mode, "newtab", StringComparison.OrdinalIgnoreCase))
            {
                return "newtab";
            }

            if (string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return "custom";
            }

            return "asys";
        }

        private static bool IsAboutAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string value = address.Trim().ToLowerInvariant();
            return value == "about:about" || value == "vantage:about";
        }

        private static bool IsDiagnosticsAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string value = address.Trim().ToLowerInvariant();
            return value == "about:diag" || value == "about:diagnostics" || value == "vantage:diag";
        }

        private static string NormalizeNewTabMode(string mode)
        {
            if (string.Equals(mode, "blank", StringComparison.OrdinalIgnoreCase))
            {
                return "blank";
            }

            if (string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return "custom";
            }

            if (string.Equals(mode, "home", StringComparison.OrdinalIgnoreCase))
            {
                return "home";
            }

            return "builtin";
        }

        private static string NormalizeCustomUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            url = url.Trim();

            if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                url.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return url;
            }

            return "https://" + url;
        }

        private void CloseActiveTab()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            CloseTabById(tab.Id);
        }

        private void NavigateTo(string address)
        {
            NavigateTo(address, false);
        }

        private void NavigateTo(string address, bool autoComplete)
        {
            ClearAutoFallback();

            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            if (IsDiagnosticsAddress(address))
            {
                ShowDiagnostics();
                return;
            }

            if (IsAboutAddress(address))
            {
                ShowAboutDialog();
                return;
            }

            string internalPage = NormalizeInternalPage(address);
            if (internalPage != null)
            {
                ShowInternalPage(internalPage);
                return;
            }

            string url = UrlHelper.Normalize(address, _settings.SearchEngine, _settings.CustomSearchEngines);
            if (url == null)
            {
                return;
            }

            if (autoComplete && UrlHelper.IsAutoSchemeCandidate(address))
            {
                string[] candidates = UrlHelper.BuildFallbackChain(url);

                if (candidates != null && candidates.Length > 1)
                {
                    _autoFallbacks = candidates;
                    _autoFallbackTried = new bool[candidates.Length];
                    _autoFallbackIndex = 0;
                    _autoFallbackTried[0] = true;
                    _autoFallbackTabId = tab.Id;

                    Log.Info("Navigate (auto): " + candidates[0] + " candidates=" + candidates.Length);
                    tab.Navigate(candidates[0]);
                    return;
                }
            }

            Log.Info("Navigate: " + url);
            tab.Navigate(url);
        }

        private void ClearAutoFallback()
        {
            _autoFallbacks = null;
            _autoFallbackTried = null;
            _autoFallbackIndex = 0;
            _autoFallbackTabId = 0;
        }

        private void GoBack()
        {
            BrowserTab tab = ActiveTab;
            if (tab != null)
            {
                tab.GoBack();
            }
        }

        private void GoForward()
        {
            BrowserTab tab = ActiveTab;
            if (tab != null)
            {
                tab.GoForward();
            }
        }

        private void Reload()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            // 内置页是 HTML 快照，F5 重新生成，而不是重载旧的 data: 地址。
            string url = tab.Url;
            if (NormalizeInternalPage(url) != null)
            {
                ShowInternalPage(url);
                return;
            }

            tab.Reload();
        }

        private void StopLoading()
        {
            BrowserTab tab = ActiveTab;
            if (tab != null)
            {
                tab.Stop();
            }
        }

    }
}
