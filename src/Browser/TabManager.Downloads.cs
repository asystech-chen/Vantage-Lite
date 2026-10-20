using System;
using System.IO;
using Microsoft.Web.WebView2.Core;
using VantageLite.Diagnostics;

namespace VantageLite.Browser
{
    /// <summary>
    /// TabManager 的下载目录同步（partial）：把设置里的保存目录写到 WebView2 Profile。
    /// 依赖：CoreWebView2Profile / Log。改动注意：同一 user data 目录下的 Profile 共享，
    /// 逐标签设置是幂等的；空目录表示使用 WebView2 默认（系统下载文件夹）。
    /// </summary>
    public sealed partial class TabManager
    {
        private string _downloadDirectory;
        private bool _downloadDirectoryApplied;

        /// <summary>
        /// 设置下载目录；null/空字符串表示恢复 WebView2 默认目录。
        /// </summary>
        public void SetDownloadDirectory(string path)
        {
            _downloadDirectory = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
            _downloadDirectoryApplied = false;

            if (_downloadDirectory != null)
            {
                try
                {
                    Directory.CreateDirectory(_downloadDirectory);
                }
                catch (Exception ex)
                {
                    Log.Warn("创建下载目录失败：" + Log.Describe(ex));
                    return;
                }
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                ApplyDownloadDirectory(_tabs[i]);
            }
        }

        /// <summary>
        /// 标签 WebView2 就绪后调用；Profile 未就绪时静默跳过。
        /// </summary>
        internal void ApplyDownloadDirectory(BrowserTab tab)
        {
            if (tab == null || _downloadDirectoryApplied)
            {
                return;
            }

            CoreWebView2Profile profile = tab.Profile;
            if (profile == null)
            {
                return;
            }

            try
            {
                // 空设置表示系统默认：显式写 Downloads，避免部分运行时对 null 抛异常。
                profile.DefaultDownloadFolderPath = _downloadDirectory ?? DefaultDownloadDirectory();
                _downloadDirectoryApplied = true;
            }
            catch (Exception ex)
            {
                Log.Warn("设置下载目录失败：" + Log.Describe(ex));
            }
        }

        private static string DefaultDownloadDirectory()
        {
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(profile))
                {
                    return Path.Combine(profile, "Downloads");
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
