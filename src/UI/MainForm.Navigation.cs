using System;
using VantageLite.Browser;
using VantageLite.Diagnostics;

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
                await _tabManager.CreateTabAsync("about:blank");
                _navigationBar.FocusAddressBar();
            }
            catch (Exception ex)
            {
                Log.Error("Create tab failed", ex);
            }
        }

        private async void CloseActiveTab()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            await _tabManager.CloseTabAsync(tab.Id);
        }

        private void NavigateTo(string address)
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                return;
            }

            string internalPage = NormalizeInternalPage(address);
            if (internalPage != null)
            {
                ShowInternalPage(internalPage);
                return;
            }

            string url = UrlHelper.Normalize(address);
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
            if (tab != null)
            {
                tab.Reload();
            }
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
