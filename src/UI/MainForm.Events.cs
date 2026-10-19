using System;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Diagnostics;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的事件处理：标签管理器事件、活动标签事件转发。
    /// 依赖：TabManager / BrowserTab。改动注意：只处理活动标签的 UI 更新，后台标签只更新标签条。
    /// </summary>
    public sealed partial class MainForm
    {
        private void TabManager_TabsChanged(object sender, EventArgs e)
        {
            if (_tabStrip != null && _tabManager != null)
            {
                _tabStrip.Sync(_tabManager.Tabs, _tabManager.ActiveTab);
            }
        }

        private void TabManager_ActiveTabChanged(object sender, EventArgs e)
        {
            AttachActiveTab();
            UpdateFromActiveTab();
        }

        private void TabManager_Empty(object sender, EventArgs e)
        {
            // 延迟关闭，避免在 CloseTabAsync 执行中途销毁控件。
            BeginInvoke(new Action(Close));
        }

        private void TabStrip_TabSelected(object sender, int id)
        {
            ActivateTabById(id);
        }

        private void TabStrip_TabCloseRequested(object sender, int id)
        {
            CloseTabById(id);
        }

        private void TabStrip_NewTabRequested(object sender, EventArgs e)
        {
            CreateNewTab();
        }

        private async void ActivateTabById(int id)
        {
            if (_tabManager != null)
            {
                await _tabManager.ActivateTabAsync(id);
            }
        }

        private async void CloseTabById(int id)
        {
            if (_tabManager != null)
            {
                await _tabManager.CloseTabAsync(id);
            }
        }

        private void AttachActiveTab()
        {
            BrowserTab next = ActiveTab;
            if (_attachedTab == next)
            {
                return;
            }

            DetachTab(_attachedTab);
            _attachedTab = next;
            AttachTab(next);
        }

        private void AttachTab(BrowserTab tab)
        {
            if (tab == null)
            {
                return;
            }

            tab.TitleChanged += Tab_TitleChanged;
            tab.AddressChanged += Tab_AddressChanged;
            tab.NavigationStateChanged += Tab_NavigationStateChanged;
            tab.LoadingChanged += Tab_LoadingChanged;
            tab.ProcessFailed += Tab_ProcessFailed;
        }

        private void DetachTab(BrowserTab tab)
        {
            if (tab == null)
            {
                return;
            }

            tab.TitleChanged -= Tab_TitleChanged;
            tab.AddressChanged -= Tab_AddressChanged;
            tab.NavigationStateChanged -= Tab_NavigationStateChanged;
            tab.LoadingChanged -= Tab_LoadingChanged;
            tab.ProcessFailed -= Tab_ProcessFailed;
        }

        private void Tab_TitleChanged(object sender, string title)
        {
            if (sender == ActiveTab)
            {
                UpdateWindowTitle();
            }
        }

        private void Tab_AddressChanged(object sender, string url)
        {
            if (sender == ActiveTab)
            {
                _navigationBar.SetAddress(url);
            }
        }

        private void Tab_NavigationStateChanged(object sender, EventArgs e)
        {
            if (sender == ActiveTab)
            {
                UpdateNavigationState();
            }
        }

        private void Tab_LoadingChanged(object sender, bool loading)
        {
            if (sender == ActiveTab)
            {
                _navigationBar.SetLoading(loading);
            }
        }

        private async void TabManager_NewWindowRequested(object sender, string url)
        {
            if (_tabManager == null)
            {
                return;
            }

            try
            {
                await _tabManager.CreateTabAsync(url);
            }
            catch (Exception ex)
            {
                Log.Error("Open new tab failed: " + url, ex);
            }
        }

        private void Tab_ProcessFailed(object sender, string kind)
        {
            if (sender != ActiveTab)
            {
                return;
            }

            MessageBox.Show(
                this,
                "浏览器渲染进程异常退出：\n" + kind,
                "Vantage Lite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

    }
}
