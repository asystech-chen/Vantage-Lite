using System;
using System.IO;

namespace VantageLite.App
{
    /// <summary>
    /// 便携路径约定：所有数据都放在 exe 同目录。
    /// 依赖：无。改动注意：exe 必须放在可写目录，不要放 Program Files。
    /// </summary>
    public static class AppPaths
    {
        public static string BaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>
        /// 程序根目录。配置、日志、WebView2 profile 都从这里派生。
        /// </summary>
        public static string DataRoot
        {
            get { return BaseDirectory; }
        }

        public static string DefaultUserDataDir
        {
            get { return Path.Combine(DataRoot, "UserData"); }
        }

        /// <summary>
        /// 持久化数据目录：书签、历史等。
        /// </summary>
        public static string DataDirectory
        {
            get { return Path.Combine(DataRoot, "data"); }
        }

        public static string DefaultLogDir
        {
            get { return Path.Combine(DataRoot, "Logs"); }
        }

        /// <summary>
        /// 预留：设置文件固定放在 exe 同目录。
        /// </summary>
        public static string ConfigFile
        {
            get { return Path.Combine(DataRoot, "settings.json"); }
        }

        public static string ResolveUserDataDir(string explicitDir)
        {
            string dir = string.IsNullOrWhiteSpace(explicitDir)
                ? DefaultUserDataDir
                : Expand(explicitDir);

            return Path.GetFullPath(dir);
        }

        public static string ResolveLogFile(string explicitFile)
        {
            if (!string.IsNullOrWhiteSpace(explicitFile))
            {
                return Path.GetFullPath(Expand(explicitFile));
            }

            string fileName = "vantage-lite-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
            return Path.GetFullPath(Path.Combine(DefaultLogDir, fileName));
        }

        public static string Expand(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        }

        public static void EnsureDirectory(string dir)
        {
            Directory.CreateDirectory(dir);
        }

        public static void EnsureDirectoryForFile(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
