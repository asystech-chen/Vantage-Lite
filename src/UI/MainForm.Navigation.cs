using System;
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

            Log.Info("Navigate: " + url);
            tab.Navigate(url);
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
