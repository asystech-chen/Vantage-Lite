using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;

namespace VantageLite.Browser
{
    /// <summary>
    /// 告诉 WebView2 去哪里找单文件解压出来的原生 Loader。
    /// 依赖：EmbeddedAssemblies。改动注意：必须在任何 CoreWebView2Environment 静态调用之前执行。
    /// </summary>
    public static class WebView2Bootstrap
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ConfigureLoader()
        {
            string dir = EmbeddedAssemblies.LoaderDirectory;
            if (!string.IsNullOrEmpty(dir))
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(dir);
            }
        }
    }
}
