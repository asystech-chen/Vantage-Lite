using System;
using System.IO;

namespace VantageLite.App
{
    /// <summary>
    /// 路径约定：默认写 LocalAppData；exe 同目录存在 portable.txt 时改为便携模式。
    /// 依赖：无（仅 System.IO）。改动注意：不要在这里创建目录，创建交给 Ensure*。
    /// </summary>
    public static class AppPaths
    {
        public static string BaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(BaseDirectory, "portable.txt")); }
        }

        public static string DataRoot
        {
            get
            {
                if (IsPortable)
                {
                    return Path.Combine(BaseDirectory, "data");
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VantageLite");
            }
        }

        public static string DefaultUserDataDir
        {
            get { return Path.Combine(DataRoot, "UserData"); }
        }

        public static string DefaultLogDir
        {
            get { return Path.Combine(DataRoot, "Logs"); }
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
