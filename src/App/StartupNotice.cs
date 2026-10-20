using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using VantageLite.Diagnostics;

namespace VantageLite.App
{
    /// <summary>
    /// 启动提醒的状态判定与外部动作。
    /// 依赖：AppPaths / Log。改动注意：首次运行判定必须在任何数据目录写入之前调用。
    /// </summary>
    public static class StartupNotice
    {
        /// <summary>
        /// Microsoft 官方 Evergreen 独立安装包（x64，fwlink 始终指向当前稳定版）。
        /// </summary>
        public const string WebViewDownloadUrl = "https://go.microsoft.com/fwlink/?linkid=2124701";

        private const string RuntimeClientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        // 保持 IE 自动化引用到进程退出，避免释放后窗口被关闭（进程随即退出）。
        private static object _fallbackExplorer;

        /// <summary>
        /// 便携数据目录尚不存在 → 视为在本目录首次启动。
        /// MainForm 构造时 BookmarkStore 会创建 data/，因此要在创建窗口之前判断。
        /// </summary>
        public static bool IsFirstRunInDirectory
        {
            get { return !Directory.Exists(AppPaths.DataDirectory); }
        }

        /// <summary>
        /// 是否残留 WebView2 的安装注册信息。
        /// 用于区分「从未安装」和「卸载不完整 / 注册信息损坏」——后者安装包会提示已安装。
        /// </summary>
        public static bool IsRuntimeRegistered
        {
            get
            {
                return KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + RuntimeClientId)
                    || KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\EdgeUpdate\Clients\" + RuntimeClientId)
                    || KeyExists(RegistryHive.CurrentUser, @"Software\Microsoft\EdgeUpdate\Clients\" + RuntimeClientId)
                    || KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView")
                    || KeyExists(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView");
            }
        }

        private static bool KeyExists(RegistryHive hive, string path)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey key = baseKey.OpenSubKey(path))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 用系统默认浏览器打开 WebView2 下载页；没有默认浏览器时回退到 IE。
        /// </summary>
        public static bool OpenWebViewDownloadPage()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(WebViewDownloadUrl);
                info.UseShellExecute = true;
                Process.Start(info);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("默认浏览器打开失败：" + ex.Message);
            }

            return OpenWithInternetExplorer(WebViewDownloadUrl);
        }

        /// <summary>
        /// 回退：通过 InternetExplorer.Application 启动 IE。
        /// 所有 Windows 都保留了该 COM 注册，即使没有默认浏览器也能显示页面。
        /// </summary>
        private static bool OpenWithInternetExplorer(string url)
        {
            object explorer = null;

            try
            {
                Type type = Type.GetTypeFromProgID("InternetExplorer.Application");

                if (type == null)
                {
                    Log.Warn("InternetExplorer.Application 未注册");
                    return false;
                }

                explorer = Activator.CreateInstance(type);
                type.InvokeMember("Visible", BindingFlags.SetProperty, null, explorer, new object[] { true });
                type.InvokeMember("Navigate", BindingFlags.InvokeMethod, null, explorer, new object[] { url });
                _fallbackExplorer = explorer;
                Log.Info("已通过 Internet Explorer 打开下载页");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("通过 IE 打开下载页失败：" + ex.Message);
                return false;
            }
        }
    }
}
