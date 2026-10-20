using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.Browser
{
    /// <summary>
    /// BrowserTab 的 WebView2 创建与恢复逻辑（partial）。
    /// 依赖：WebView2 / Log。改动注意：创建必须 single-flight，快速切标签时不能并发建两个 WebView2。
    /// </summary>
    public sealed partial class BrowserTab
    {
        private readonly object _liveGate = new object();
        private Task _liveTask;

        /// <summary>
        /// 确保 WebView2 已创建；并发调用共享同一个初始化任务。
        /// </summary>
        public Task EnsureLiveAsync()
        {
            lock (_liveGate)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                if (_webView != null && !_webView.IsDisposed)
                {
                    return Task.CompletedTask;
                }

                if (_liveTask != null && !_liveTask.IsCompleted)
                {
                    return _liveTask;
                }

                _liveTask = CreateLiveAsync();
                return _liveTask;
            }
        }

        private async Task CreateLiveAsync()
        {
            Log.Info("[tab " + _id + "] creating WebView2");

            WebView2 view = new WebView2();
            view.Dock = DockStyle.Fill;
            view.DefaultBackgroundColor = ThemeManager.Current.WebViewBackground;

            try
            {
                Controls.Add(view);
                await view.EnsureCoreWebView2Async(_environment);

                // 初始化期间标签可能已被关闭或丢弃。
                if (_disposed)
                {
                    Controls.Remove(view);
                    view.Dispose();
                    return;
                }

                CoreWebView2 core = view.CoreWebView2;

                _webView = view;
                _core = core;
                ConfigureCore(core);
                _isSuspended = false;
                _isThrottled = false;
                _statusBarSuppressed = false;

                // 页面内表单改动标记，用于丢弃前的脏数据保护。
                await core.AddScriptToExecuteOnDocumentCreatedAsync(DirtyFormScript.Source);

                // 快捷键兜底：页面聚焦时 WinForms 收不到按键。
                await core.AddScriptToExecuteOnDocumentCreatedAsync(ShortcutScript.Source);
            }
            catch (Exception)
            {
                // 初始化失败时不能留下半初始化的控件引用。
                if (ReferenceEquals(_webView, view))
                {
                    _webView = null;
                    _core = null;
                    _isSuspended = false;
                }

                Controls.Remove(view);
                view.Dispose();
                throw;
            }

            if (_disposed || !ReferenceEquals(_webView, view))
            {
                return;
            }

            // 内置页作为首个页面：直接 NavigateToString，避免先加载 about:blank 再跳转。
            // 丢弃后恢复时没有缓存 HTML，由 provider 按当前设置/书签重新生成。
            if (!string.IsNullOrEmpty(_internalAddress))
            {
                string address = _internalAddress;
                string html = _pendingInternalHtml;

                _pendingInternalAddress = null;
                _pendingInternalHtml = null;

                if (html == null && InternalHtmlProvider != null)
                {
                    try
                    {
                        html = InternalHtmlProvider(address);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("[tab " + _id + "] internal page rebuild failed: " + Log.Describe(ex));
                    }
                }

                if (!string.IsNullOrEmpty(html))
                {
                    ShowInternalPage(address, html);
                    return;
                }
            }

            string url = _restorePending ? _url : _pendingUrl;
            _restorePending = false;
            _pendingUrl = null;

            if (!string.IsNullOrEmpty(url))
            {
                Navigate(url);
            }
        }
    }
}
