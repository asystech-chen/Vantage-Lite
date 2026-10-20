using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace VantageLite.Diagnostics
{
    /// <summary>
    /// 启动时采集环境信息，并区分“本程序新增的 WebView2 进程”和整机总量。
    /// 依赖：WebView2 Core。改动注意：CaptureBaseline 必须在创建 WebView2 环境之前调用。
    /// </summary>
    public static class DiagnosticsInfo
    {
        private static readonly HashSet<int> BaselinePids = new HashSet<int>();

        /// <summary>
        /// 记录启动时已经存在的 msedgewebview2 进程，后续统计时排除它们。
        /// </summary>
        public static void CaptureBaseline()
        {
            BaselinePids.Clear();
            Process[] processes = Process.GetProcessesByName("msedgewebview2");

            for (int i = 0; i < processes.Length; i++)
            {
                try
                {
                    BaselinePids.Add(processes[i].Id);
                }
                catch
                {
                }
                finally
                {
                    processes[i].Dispose();
                }
            }
        }

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
            sb.AppendLine("Memory: " + GetMemorySummary());
            sb.AppendLine("WebView2 runtime: " + GetWebView2Version());
            sb.AppendLine("-------------------");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// host = 本程序宿主；ours = 本程序启动后新增的 WebView2 进程；total = 整机全部。
        /// 使用私有内存，避免 WorkingSet 重复计算共享页。
        /// </summary>
        public static string GetMemorySummary()
        {
            long oursBytes = 0;
            long totalBytes = 0;
            long oursWorkingSet = 0;
            int oursCount = 0;
            int totalCount = 0;

            try
            {
                Process[] processes = Process.GetProcessesByName("msedgewebview2");
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        long bytes = processes[i].PrivateMemorySize64;
                        totalBytes += bytes;
                        totalCount++;

                        if (!BaselinePids.Contains(processes[i].Id))
                        {
                            oursBytes += bytes;
                            oursCount++;
                            oursWorkingSet += processes[i].WorkingSet64;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        processes[i].Dispose();
                    }
                }
            }
            catch
            {
            }

            // ours=私有提交（偏大），ws=工作集（与任务管理器接近）。
            return "host=" + (GetHostPrivateBytes() / 1024 / 1024) + "MB"
                + ", ours=" + (oursBytes / 1024 / 1024) + "MB/" + oursCount + "p"
                + ", ws=" + (oursWorkingSet / 1024 / 1024) + "MB"
                + ", total=" + (totalBytes / 1024 / 1024) + "MB/" + totalCount + "p";
        }

        private static long GetHostPrivateBytes()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    process.Refresh();
                    return process.PrivateMemorySize64;
                }
            }
            catch
            {
                return 0;
            }
        }

        private static string GetExecutablePath()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                return process.MainModule.FileName;
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
