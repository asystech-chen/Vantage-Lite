using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VantageLite.App
{
    /// <summary>
    /// Windows 已知文件夹解析。依赖：shell32。
    /// 改动注意：默认下载目录必须用 FOLDERID_Downloads，不能用 %USERPROFILE%\Downloads，
    /// 否则用户把「下载」文件夹改到其它盘后会写错位置。
    /// </summary>
    internal static class KnownFolders
    {
        private static readonly Guid DownloadsFolderId =
            new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(
            ref Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppszPath);

        /// <summary>
        /// 系统「下载」文件夹的实际路径（支持用户重定向到其它磁盘）。
        /// </summary>
        public static string DownloadsPath()
        {
            IntPtr pointer = IntPtr.Zero;

            try
            {
                Guid id = DownloadsFolderId;

                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out pointer) == 0 &&
                    pointer != IntPtr.Zero)
                {
                    string path = Marshal.PtrToStringUni(pointer);

                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeCoTaskMem(pointer);
                    }
                    catch
                    {
                    }
                }
            }

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

            return AppPaths.DataDirectory;
        }
    }
}
