namespace VantageLite.Browser
{
    /// <summary>
    /// 注入到每个页面的脏表单标记脚本。
    /// 依赖：无。改动注意：只做轻量标记，不读取或上传表单内容。
    /// </summary>
    internal static class DirtyFormScript
    {
        public const string Source = @"
(function () {
    if (window.__vantageDirtyHook) { return; }
    window.__vantageDirtyHook = true;
    window.__vantageDirty = false;
    var mark = function () { window.__vantageDirty = true; };
    document.addEventListener('input', mark, true);
    document.addEventListener('change', mark, true);
    document.addEventListener('submit', function () { window.__vantageDirty = false; }, true);
})();
";
    }
}
