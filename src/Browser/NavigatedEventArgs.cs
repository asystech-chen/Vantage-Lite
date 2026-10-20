using System;

namespace VantageLite.Browser
{
    /// <summary>
    /// 页面导航完成事件参数。依赖：无。改动注意：只在顶层文档成功加载后触发。
    /// </summary>
    public sealed class NavigatedEventArgs : EventArgs
    {
        public NavigatedEventArgs(string url, string title)
        {
            Url = url;
            Title = title;
        }

        public string Url { get; private set; }

        public string Title { get; private set; }
    }
}
