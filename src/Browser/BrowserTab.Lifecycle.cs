using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.Browser
{
    /// <summary>
    /// BrowserTab 的丢弃与销毁逻辑（partial）。
    /// 依赖：Log。改动注意：丢弃前必须保存 URL 和标题，恢复时由 EnsureLiveAsync 重建。
    /// </summary>
    public sealed partial class BrowserTab
    {
        /// <summary>
        /// 跟随系统明暗主题：窗口底色 + 网页 prefers-color-scheme。
        /// </summary>
        public void ApplyTheme()
        {
            BackColor = ThemeManager.Current.WebViewBackground;

            if (_webView != null && !_webView.IsDisposed)
            {
                _webView.DefaultBackgroundColor = ThemeManager.Current.WebViewBackground;
            }

            if (_core != null && !_isSuspended && !_disposed)
            {
                _core.Profile.PreferredColorScheme = ThemeManager.IsDark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
            }
        }

        /// <summary>
        /// 挂起后台标签：保留 DOM/表单/滚动位置，只暂停脚本和渲染。
        /// 调用前控件必须不可见，否则 TrySuspendAsync 会抛 COMException。
        /// </summary>
        public async Task<bool> SuspendAsync()
        {
            DropDisposedWebView();

            if (_webView == null || _core == null || _isSuspended || _disposed)
            {
                return false;
            }

            try
            {
                // 先记录脏表单状态，挂起后不能再执行脚本查询。
                _hasUnsavedInput = await IsDirtyAsync();

                DropDisposedWebView();
                if (_webView == null || _core == null)
                {
                    return false;
                }

                bool suspended = await _core.TrySuspendAsync();
                _isSuspended = suspended;
                Log.Info("[tab " + _id + "] suspend: " + (suspended ? "ok" : "refused"));
                return suspended;
            }
            catch (Exception ex)
            {
                if (_disposed || _webView == null || _webView.IsDisposed)
                {
                    Log.Debug("[tab " + _id + "] suspend skipped (disposed)");
                    return false;
                }

                Log.Warn("[tab " + _id + "] suspend failed: " + ex.Message);
                return false;
            }
        }

        public void Resume()
        {
            DropDisposedWebView();

            if (_webView == null || _disposed)
            {
                return;
            }

            if (_core != null && _core.IsSuspended)
            {
                _core.Resume();
            }

            _isSuspended = false;
        }

        /// <summary>
        /// 页面里是否有未提交的表单改动。
        /// </summary>
        public async Task<bool> IsDirtyAsync()
        {
            DropDisposedWebView();

            if (_core == null || _disposed)
            {
                return false;
            }

            try
            {
                string result = await _core.ExecuteScriptAsync("window.__vantageDirty === true");
                return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 真正销毁 WebView2：先保存 URL、标题和滚动位置。
        /// </summary>
        public async Task<bool> DiscardAsync()
        {
            DropDisposedWebView();

            if (_webView == null || _disposed)
            {
                return false;
            }

            if (_core != null)
            {
                if (_core.IsSuspended)
                {
                    _core.Resume();
                }

                _url = _core.Source;
                _title = _core.DocumentTitle;
                await CaptureScrollAsync();
            }

            _pendingUrl = _url;
            _restorePending = true;
            _isSuspended = false;

            Controls.Remove(_webView);
            _webView.Dispose();
            _webView = null;
            _core = null;

            Log.Info("[tab " + _id + "] discarded: " + _url);

            EventHandler<string> handler = TitleChanged;
            if (handler != null)
            {
                handler(this, DisplayTitle);
            }

            return true;
        }

        private async Task CaptureScrollAsync()
        {
            try
            {
                string result = await _core.ExecuteScriptAsync("window.scrollX + ',' + window.scrollY");
                string text = result == null ? string.Empty : result.Trim('"');
                string[] parts = text.Split(',');
                int x;
                int y;

                if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                {
                    _pendingScrollX = x;
                    _pendingScrollY = y;
                    _hasPendingScroll = true;
                }
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            if (disposing && _webView != null)
            {
                Controls.Remove(_webView);
                _webView.Dispose();
                _webView = null;
                _core = null;
            }

            base.Dispose(disposing);
        }

        private void DropDisposedWebView()
        {
            if (_webView != null && _webView.IsDisposed)
            {
                _webView = null;
                _core = null;
                _isSuspended = false;
            }
        }

    }
}
