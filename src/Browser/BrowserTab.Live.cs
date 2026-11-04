using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VantageLite.Diagnostics;
using VantageLite.Theming;
using VantageLite.UI;

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
        private CoreWebView2Controller _controller;

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

        /// <summary>
        /// 订阅 WebView2 的栅格化缩放：它是显示器 DPI 与文本缩放的合成值，
        /// 宿主界面与网页内容共用这一来源，不再额外叠加 Windows 设置。
        /// </summary>
        private void AttachRasterizationScale(WebView2 view)
        {
            try
            {
                // WinForms 包装控件不公开 Controller，只能从内部字段取；
                // 取不到就不订阅，界面退回 DeviceDpi 缩放。
                CoreWebView2Controller controller = FindController(view);
                if (controller == null)
                {
                    Log.Warn("[tab " + _id + "] controller not found; ui scale falls back to dpi");
                    return;
                }

                DetachRasterizationScale();
                _controller = controller;
                controller.RasterizationScaleChanged += Controller_RasterizationScaleChanged;
                WindowChrome.ReportRasterizationScale(controller.RasterizationScale);
                Log.Info("[tab " + _id + "] rasterization scale = " + controller.RasterizationScale.ToString("0.###"));
            }
            catch (Exception ex)
            {
                Log.Warn("[tab " + _id + "] rasterization scale unavailable: " + Log.Describe(ex));
            }
        }

        private static CoreWebView2Controller FindController(WebView2 view)
        {
            if (view == null)
            {
                return null;
            }

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            System.Reflection.FieldInfo field = typeof(WebView2).GetField("_coreWebView2Controller", Flags);

            if (field != null)
            {
                CoreWebView2Controller controller = field.GetValue(view) as CoreWebView2Controller;
                if (controller != null)
                {
                    return controller;
                }
            }

            // 字段名随版本变化时按类型扫一遍，避免直接失效。
            System.Reflection.FieldInfo[] fields = typeof(WebView2).GetFields(Flags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (typeof(CoreWebView2Controller).IsAssignableFrom(fields[i].FieldType))
                {
                    CoreWebView2Controller controller = fields[i].GetValue(view) as CoreWebView2Controller;
                    if (controller != null)
                    {
                        return controller;
                    }
                }
            }

            return null;
        }

        private void DetachRasterizationScale()
        {
            if (_controller == null)
            {
                return;
            }

            try
            {
                _controller.RasterizationScaleChanged -= Controller_RasterizationScaleChanged;
            }
            catch
            {
            }

            _controller = null;
        }

        private void Controller_RasterizationScaleChanged(object sender, object e)
        {
            CoreWebView2Controller controller = sender as CoreWebView2Controller;

            if (controller == null)
            {
                return;
            }

            try
            {
                WindowChrome.ReportRasterizationScale(controller.RasterizationScale);
            }
            catch (Exception ex)
            {
                Log.Warn("[tab " + _id + "] rasterization scale change failed: " + Log.Describe(ex));
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

                if (_privateMode)
                {
                    CoreWebView2ControllerOptions controllerOptions = _environment.CreateCoreWebView2ControllerOptions();
                    controllerOptions.IsInPrivateModeEnabled = true;
                    await view.EnsureCoreWebView2Async(_environment, controllerOptions);
                }
                else
                {
                    await view.EnsureCoreWebView2Async(_environment);
                }

                // 初始化期间标签可能已被关闭或丢弃。
                if (_disposed)
                {
                    Controls.Remove(view);
                    view.Dispose();
                    return;
                }

                CoreWebView2 core = view.CoreWebView2;

                // 防止运行时忽略 InPrivate 标志时，隐私标签悄悄落到普通 Profile。
                if (_privateMode && !core.Profile.IsInPrivateModeEnabled)
                {
                    throw new InvalidOperationException("WebView2 InPrivate 模式不可用");
                }

                _webView = view;
                _core = core;
                ConfigureCore(core);
                AttachRasterizationScale(view);
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
