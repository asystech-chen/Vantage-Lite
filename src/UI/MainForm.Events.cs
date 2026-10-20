using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
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
            ApplyInternalPageTheme(ActiveTab);

            BrowserTab active = ActiveTab;

            if (_autoFallbacks != null &&
                (active == null || active.Id != _autoFallbackTabId))
            {
                ClearAutoFallback();
            }
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
            if (_tabManager == null)
            {
                return;
            }

            try
            {
                await _tabManager.CloseTabAsync(id);
            }
            catch (Exception ex)
            {
                Log.Error("Close tab failed", ex);
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
            tab.NavigationFailed += Tab_NavigationFailed;
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
            tab.NavigationFailed -= Tab_NavigationFailed;
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
                UpdateBookmarkState();
            }
        }

        /// <summary>
        /// 地址栏自动补全：HTTPS 失败后按候选序列尝试 www / http。
        /// </summary>
        private void Tab_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            BrowserTab tab = sender as BrowserTab;

            if (tab == null || _autoFallbacks == null || _autoFallbackTried == null)
            {
                return;
            }

            if (tab.Id != _autoFallbackTabId)
            {
                return;
            }

            if (_autoFallbackIndex < 0 || _autoFallbackIndex >= _autoFallbacks.Length)
            {
                ClearAutoFallback();
                return;
            }

            if (!string.Equals(e.Url, _autoFallbacks[_autoFallbackIndex], StringComparison.OrdinalIgnoreCase))
            {
                ClearAutoFallback();
                return;
            }

            int next = FindNextAutoFallback(e.Status);

            if (next < 0)
            {
                ClearAutoFallback();
                return;
            }

            _autoFallbackIndex = next;
            _autoFallbackTried[next] = true;

            Log.Info("[auto] " + e.Status + " -> " + _autoFallbacks[next]);
            tab.Navigate(_autoFallbacks[next]);
        }

        /// <summary>
        /// 解析失败类型：DNS 失败尝试同协议的 www 变体，连接失败尝试 http 变体。
        /// 证书错误不降级、不绕过。
        /// </summary>
        private int FindNextAutoFallback(CoreWebView2WebErrorStatus status)
        {
            if (_autoFallbacks == null || _autoFallbackTried == null)
            {
                return -1;
            }

            bool dnsFailure = status == CoreWebView2WebErrorStatus.HostNameNotResolved;
            bool connectFailure =
                status == CoreWebView2WebErrorStatus.CannotConnect ||
                status == CoreWebView2WebErrorStatus.Timeout ||
                status == CoreWebView2WebErrorStatus.ServerUnreachable;

            if (!dnsFailure && !connectFailure)
            {
                return -1;
            }

            for (int i = 0; i < _autoFallbacks.Length; i++)
            {
                if (_autoFallbackTried[i])
                {
                    continue;
                }

                Uri uri;
                if (!Uri.TryCreate(_autoFallbacks[i], UriKind.Absolute, out uri))
                {
                    continue;
                }

                if (dnsFailure)
                {
                    if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                        uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
                else if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void TabManager_Navigated(object sender, NavigatedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.Url))
            {
                return;
            }

            _history.Add(e.Url, e.Title);
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

        private async void MainForm_Resize(object sender, EventArgs e)
        {
            if (_tabManager == null)
            {
                return;
            }

            bool minimized = WindowState == FormWindowState.Minimized;

            if (minimized != _minimized)
            {
                _minimized = minimized;

                try
                {
                    await _tabManager.SetMinimizedAsync(minimized);
                }
                catch (Exception ex)
                {
                    Log.Error("window state transition failed", ex);
                }
            }

            if (minimized)
            {
                return;
            }

            // 缩放期间先关掉链接预览条，避免它停在旧位置；停止缩放后再恢复。
            BrowserTab active = ActiveTab;
            if (active != null)
            {
                active.SuppressStatusBar();
            }

            _resizeSettleTimer.Stop();
            _resizeSettleTimer.Start();
        }

        private void ResizeSettleTimer_Tick(object sender, EventArgs e)
        {
            _resizeSettleTimer.Stop();

            BrowserTab active = ActiveTab;
            if (active != null)
            {
                active.RestoreStatusBar();
            }
        }

        private void Tab_ProcessFailed(object sender, string kind)
        {
            if (sender != ActiveTab)
            {
                return;
            }

            MessageDialog.Show(
                this,
                "浏览器渲染进程异常退出：\n" + kind,
                "Vantage Lite",
                MessageBoxButtons.OK);
        }

    }
}
