using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace VantageLite.Diagnostics
{
    /// <summary>
    /// 启动时采集环境信息，写进日志帮助定位问题。
    /// 依赖：WebView2 Core。改动注意：任何一项探测失败都不能抛异常。
    /// </summary>
    public static class DiagnosticsInfo
    {
        public static string Collect()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Diagnostics ---");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            sb.AppendLine("OS: " + SafeGet(delegate { return Environment.OSVersion.VersionString; })
                + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")");
            sb.AppendLine("CLR: " + SafeGet(delegate { return Environment.Version.ToString(); }));
            sb.AppendLine("Process arch: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            sb.AppendLine("Exe: " + SafeGet(GetExecutablePath));
            sb.AppendLine("Working set: " + SafeGet(GetWorkingSetMb) + " MB");
            sb.AppendLine("WebView2 runtime: " + GetWebView2Version());
            sb.AppendLine("-------------------");
            return sb.ToString().TrimEnd();
        }

        private static string GetExecutablePath()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                return process.MainModule.FileName;
            }
        }

        private static string GetWorkingSetMb()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                return (process.WorkingSet64 / 1024 / 1024).ToString();
            }
        }

        private static string GetWebView2Version()
        {
            try
            {
                string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return string.IsNullOrEmpty(version) ? "not installed" : version;
            }
            catch (Exception ex)
            {
                return "not installed (" + ex.GetType().Name + ")";
            }
        }

        private static string SafeGet(Func<string> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
