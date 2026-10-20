namespace VantageLite.Browser
{
    /// <summary>
    /// 页面内快捷键兜底：某些环境下 WinForms 消息过滤器收不到 WebView2 的按键，
    /// 由页面脚本捕获后通过 web message 转给宿主。
    /// 依赖：无。改动注意：只拦截浏览器自己的组合，不影响页面其他快捷键。
    /// </summary>
    public static class ShortcutScript
    {
        public const string Source = @"
(function(){
  if (window.__vantageShortcuts) { return; }
  window.__vantageShortcuts = true;
  document.addEventListener('keydown', function(e){
    if (!e.ctrlKey || e.metaKey) { return; }
    var key = (e.key || '').toLowerCase();
    var name = null;
    if (e.altKey) {
      if (key === 'b' && !e.shiftKey) { name = 'bookmarks'; }
    } else if (key === 'd' && !e.shiftKey) {
      name = 'bookmark';
    } else if (key === 'b' && e.shiftKey) {
      name = 'bookmarksbar';
    }
    if (!name) { return; }
    e.preventDefault();
    e.stopPropagation();
    try {
      window.chrome.webview.postMessage(JSON.stringify({ cmd: 'shortcut', name: name }));
    } catch (err) { }
  }, true);
})();
";
    }
}
