using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的 WebView2 事件处理。与 MainForm.cs 同属一个 partial 类。
    /// 依赖：Log / NavigationBar。改动注意：这里只做状态同步和日志，不写业务分支。
    /// </summary>
    public sealed partial class MainForm
    {
        private void Core_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            _navigationBar.SetAddress(_core.Source);
            Log.Debug("Source changed: " + _core.Source);
        }

        private void Core_DocumentTitleChanged(object sender, object e)
        {
            string title = _core.DocumentTitle;
            Text = string.IsNullOrWhiteSpace(title) ? "Vantage Lite" : title + " - Vantage Lite";
        }

        private void Core_HistoryChanged(object sender, object e)
        {
            UpdateNavigationState();
        }

        private void Core_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _isLoading = true;
            _navigationBar.SetLoading(true);
            Log.Info("Navigation starting: " + e.Uri);
        }

        private void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _isLoading = false;
            _navigationBar.SetLoading(false);
            UpdateNavigationState();

            if (!e.IsSuccess)
            {
                Log.Warn("Navigation failed: " + e.WebErrorStatus);
            }
        }

        private void Core_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // M0：单标签，统一在当前视图打开；M1 改为新建标签。
            e.Handled = true;
            Log.Info("New window requested, navigating current view: " + e.Uri);
            NavigateTo(e.Uri);
        }

        private void Core_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            Log.Error("WebView2 process failed: " + e.ProcessFailedKind);
            MessageBox.Show(
                this,
                "浏览器渲染进程异常退出：\n" + e.ProcessFailedKind,
                "Vantage Lite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
