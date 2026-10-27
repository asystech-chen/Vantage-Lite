using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Downloads;

namespace VantageLite.UI
{
    /// <summary>
    /// 隐私工作区：进入时挂起普通标签，退出时销毁隐私标签与临时下载记录。
    /// 依赖：TabManager / DownloadManager / InternalPages。
    /// 改动注意：切换期间只允许当前 _tabManager 的事件更新 UI；普通标签不关闭，只挂起。
    /// </summary>
    public sealed partial class MainForm
    {
        private void WireDownloadManager(DownloadManager manager)
        {
            if (manager == null)
            {
                return;
            }

            manager.Started += Downloads_Started;
            manager.Changed += delegate
            {
                // 下载事件可能来自页面消息处理，延后重绘，避免在 WebMessage 回调里导航页面。
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            RenderDownloadsPage();
                            RefreshDownloadBadge();
                        }));
                    }
                    catch
                    {
                    }
                }
            };
        }

        private void WireTabManager(TabManager manager)
        {
            if (manager == null)
            {
                return;
            }

            manager.TabsChanged += TabManager_TabsChanged;
            manager.ActiveTabChanged += TabManager_ActiveTabChanged;
            manager.NewWindowRequested += TabManager_NewWindowRequested;
            manager.Navigated += TabManager_Navigated;
            manager.WebMessage += TabManager_WebMessage;
            manager.LastTabClosed += TabManager_LastTabClosed;
            manager.FullScreenChanged += TabManager_FullScreenChanged;
            manager.DownloadStarting += delegate(object downloadSender, CoreWebView2DownloadStartingEventArgs downloadArgs)
            {
                if (ReferenceEquals(downloadSender, _normalTabManager))
                {
                    _normalDownloads.Begin(downloadArgs);
                }
                else if (ReferenceEquals(downloadSender, _tabManager))
                {
                    _downloads.Begin(downloadArgs);
                }
            };
        }

        private async void TogglePrivacyMode()
        {
            if (_privacyTransition)
            {
                return;
            }

            if (_privacyMode)
            {
                await ExitPrivacyModeAsync();
            }
            else
            {
                await EnterPrivacyModeAsync();
            }
        }

        private async Task EnterPrivacyModeAsync()
        {
            if (_privacyMode || _privacyTransition || _normalTabManager == null || _environment == null)
            {
                return;
            }

            _privacyTransition = true;
            TabManager privateManager = null;
            DownloadManager privateDownloads = null;

            try
            {
                CloseDownloadsPanelForWorkspaceSwitch();

                privateManager = new TabManager(
                    _contentHost,
                    _environment,
                    _options.EnableDevTools,
                    _maxLiveTabs,
                    _suspendAfterSeconds,
                    _discardAfterMinutes,
                    true);
                privateManager.InternalHtmlProvider = BuildInternalPageHtml;
                privateManager.SetDownloadDirectory(_settings.DownloadDirectory);
                WireTabManager(privateManager);

                privateDownloads = new DownloadManager(
                    Path.Combine(AppPaths.DataDirectory, "downloads.json"),
                    false);
                WireDownloadManager(privateDownloads);

                _privateTabManager = privateManager;
                _privateDownloads = privateDownloads;
                _tabManager = privateManager;
                _downloads = privateDownloads;
                _privacyMode = true;
                _closedTabs.Clear();
                VantageLite.Theming.ThemeManager.SetPrivacyMode(true);
                UpdatePrivacyUi();

                // 普通工作区隐藏并挂起，退出时原样恢复。
                await _normalTabManager.SetWorkspaceActiveAsync(false);

                await CreateTabForTargetAsync(NewTabAddress);
                _navigationBar.FocusAddressBar();

                _privacyReclaimTimer.Start();
                Log.Info("[privacy] workspace entered");
            }
            catch (Exception ex)
            {
                Log.Error("[privacy] enter failed", ex);
                _privacyReclaimTimer.Stop();

                if (privateManager != null)
                {
                    _tabManager = _normalTabManager;
                    _downloads = _normalDownloads;
                    _privacyMode = false;
                    VantageLite.Theming.ThemeManager.SetPrivacyMode(false);
                    _privateTabManager = null;
                    _privateDownloads = null;
                    privateManager.Dispose();
                }

                if (privateDownloads != null)
                {
                    privateDownloads.Dispose();
                }

                UpdatePrivacyUi();

                try
                {
                    await _normalTabManager.SetWorkspaceActiveAsync(true);
                }
                catch
                {
                }

                MessageDialog.Show(
                    this,
                    "无法进入隐私浏览模式：\n" + Log.Describe(ex),
                    "Vantage Lite",
                    MessageBoxButtons.OK);
            }
            finally
            {
                _privacyTransition = false;
            }
        }

        private async Task ExitPrivacyModeAsync()
        {
            if (!_privacyMode || _privacyTransition)
            {
                return;
            }

            _privacyTransition = true;

            try
            {
                _privacyReclaimTimer.Stop();
                CloseDownloadsPanelForWorkspaceSwitch();

                TabManager privateManager = _privateTabManager;
                DownloadManager privateDownloads = _privateDownloads;

                _tabManager = _normalTabManager;
                _downloads = _normalDownloads;
                _privacyMode = false;
                _privateTabManager = null;
                _privateDownloads = null;
                _closedTabs.Clear();
                VantageLite.Theming.ThemeManager.SetPrivacyMode(false);
                UpdatePrivacyUi();

                // 销毁隐私工作区的标签与 InPrivate Profile。
                if (privateManager != null)
                {
                    try
                    {
                        CoreWebView2Profile profile = privateManager.GetProfile();

                        if (profile != null && profile.IsInPrivateModeEnabled)
                        {
                            await profile.ClearBrowsingDataAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("[privacy] clear data failed: " + Log.Describe(ex));
                    }

                    privateManager.Dispose();
                }

                if (privateDownloads != null)
                {
                    privateDownloads.Dispose();
                }

                await _normalTabManager.SetWorkspaceActiveAsync(true);

                AttachActiveTab();
                UpdateFromActiveTab();
                ApplyInternalPageTheme(ActiveTab);
                UpdateBookmarkState();

                Log.Info("[privacy] workspace exited");
            }
            catch (Exception ex)
            {
                Log.Error("[privacy] exit failed", ex);
            }
            finally
            {
                _privacyTransition = false;
            }
        }

        private void CloseDownloadsPanelForWorkspaceSwitch()
        {
            if (_downloadsForm != null && !_downloadsForm.IsDisposed)
            {
                _downloadsForm.Close();
                _downloadsForm = null;
            }
        }

        private void UpdatePrivacyUi()
        {
            if (_privacyBanner != null)
            {
                _privacyBanner.Visible = _privacyMode;
            }

            UpdateWindowTitle();
        }

        /// <summary>
        /// Vantage 隐私窗口的深蓝绿渐变，左侧画盾牌标记。
        /// </summary>
        private void PrivacyBanner_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = _privacyBanner.ClientRectangle;

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            bool dark = VantageLite.Theming.ThemeManager.IsDark;
            Color leftColor = dark ? Color.FromArgb(23, 42, 69) : Color.FromArgb(216, 232, 252);
            Color midColor = dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(216, 242, 239);
            Color rightColor = dark ? Color.FromArgb(21, 61, 58) : Color.FromArgb(216, 247, 234);
            Color textColor = dark ? Color.White : Color.FromArgb(26, 43, 60);
            Color accent = dark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(59, 130, 246);

            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect,
                leftColor,
                rightColor,
                LinearGradientMode.Horizontal))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new[] { leftColor, midColor, rightColor };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                brush.InterpolationColors = blend;
                e.Graphics.FillRectangle(brush, rect);
            }

            int size = Math.Max(10, WindowChrome.Scale(this, 14));
            int left = WindowChrome.Scale(this, 10);
            int top = Math.Max(0, (rect.Height - size) / 2);
            float half = size / 2f;

            PointF[] shield =
            {
                new PointF(left + half, top),
                new PointF(left + size, top + size * 0.18f),
                new PointF(left + size, top + size * 0.5f),
                new PointF(left + size * 0.82f, top + size * 0.82f),
                new PointF(left + half, top + size),
                new PointF(left + size * 0.18f, top + size * 0.82f),
                new PointF(left, top + size * 0.5f),
                new PointF(left, top + size * 0.18f)
            };

            using (SolidBrush icon = new SolidBrush(accent))
            {
                e.Graphics.FillPolygon(icon, shield);
            }

            using (SolidBrush hole = new SolidBrush(dark ? Color.FromArgb(30, 58, 74) : Color.FromArgb(216, 232, 252)))
            {
                int radius = Math.Max(1, WindowChrome.Scale(this, 2));
                int cx = left + size / 2;
                int cy = top + size / 2;
                e.Graphics.FillEllipse(hole, cx - radius, cy - radius, radius * 2, radius * 2);
                e.Graphics.FillRectangle(hole, cx - radius / 2, cy, Math.Max(1, radius), Math.Max(2, radius * 2));
            }

            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                e.Graphics.FillRectangle(accentBrush, 0, rect.Height - Math.Max(1, WindowChrome.Scale(this, 1)), rect.Width, Math.Max(1, WindowChrome.Scale(this, 1)));
            }

            if (_privacyLabel != null)
            {
                int textLeft = left + size + WindowChrome.Scale(this, 8);
                int textRight = _privacyExitButton == null
                    ? rect.Width - WindowChrome.Scale(this, 8)
                    : rect.Right - _privacyExitButton.Width - WindowChrome.Scale(this, 8);
                Rectangle textRect = new Rectangle(
                    textLeft,
                    0,
                    Math.Max(0, textRight - textLeft),
                    rect.Height);

                TextRenderer.DrawText(
                    e.Graphics,
                    _privacyLabel.Text,
                    _privacyLabel.Font,
                    textRect,
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);
            }
        }

        private async void PrivacyReclaimTimer_Tick(object sender, EventArgs e)
        {
            _privacyReclaimTimer.Stop();

            if (!_privacyMode || _normalTabManager == null)
            {
                return;
            }

            try
            {
                int discarded = await _normalTabManager.ReclaimWorkspaceMemoryAsync();
                Log.Info("[privacy] reclaimed normal tabs: " + discarded);
            }
            catch (Exception ex)
            {
                Log.Warn("[privacy] reclaim failed: " + Log.Describe(ex));
            }
        }
    }
}
