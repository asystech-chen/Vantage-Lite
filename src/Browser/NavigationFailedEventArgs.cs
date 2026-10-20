using System;
using Microsoft.Web.WebView2.Core;

namespace VantageLite.Browser
{
    /// <summary>
    /// 顶层导航失败事件参数（用于地址栏的 www/http 自动补全）。
    /// 依赖：WebView2.Core。改动注意：Url 是本次尝试的地址，不是失败后 WebView 仍停留的旧页面地址。
    /// </summary>
    public sealed class NavigationFailedEventArgs : EventArgs
    {
        public NavigationFailedEventArgs(string url, CoreWebView2WebErrorStatus status)
        {
            Url = url ?? string.Empty;
            Status = status;
        }

        public string Url { get; private set; }

        public CoreWebView2WebErrorStatus Status { get; private set; }
    }
}
