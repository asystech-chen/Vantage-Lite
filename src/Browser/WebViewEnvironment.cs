using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using VantageLite.App;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// 创建 WebView2 环境，并检测 Evergreen Runtime。
    /// 依赖：AppPaths / Log。改动注意：userDataFolder 必须提前创建，否则初始化会失败。
    /// </summary>
    public static class WebViewEnvironment
    {
        public static string RuntimeVersion
        {
            get
            {
                try
                {
                    return CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool IsRuntimeAvailable
        {
            get { return !string.IsNullOrEmpty(RuntimeVersion); }
        }

        public static async Task<CoreWebView2Environment> CreateAsync(CommandLineOptions options)
        {
            string runtimeVersion = RuntimeVersion;
            if (string.IsNullOrEmpty(runtimeVersion))
            {
                throw new InvalidOperationException(
                    "未检测到 WebView2 Runtime。\n\n" +
                    "请安装 Microsoft Edge WebView2 Runtime：\n" +
                    "https://developer.microsoft.com/microsoft-edge/webview2/");
            }

            string userDataDir = AppPaths.ResolveUserDataDir(options.UserDataDir);
            AppPaths.EnsureDirectory(userDataDir);

            Log.Info("WebView2 runtime: " + runtimeVersion);
            Log.Info("User data dir: " + userDataDir);

            CoreWebView2EnvironmentOptions environmentOptions = new CoreWebView2EnvironmentOptions();
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                null,
                userDataDir,
                environmentOptions);

            return environment;
        }
    }
}
