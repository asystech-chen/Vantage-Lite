using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VantageLite.App
{
    /// <summary>
    /// 单实例锁：同一用户数据目录只允许一个进程，避免 WebView2 进程组翻倍。
    /// 依赖：AppPaths。改动注意：Mutex 必须保存在静态字段，否则可能被 GC 回收导致锁失效。
    /// </summary>
    internal static class SingleInstance
    {
        private static Mutex _mutex;

        /// <summary>
        /// 尝试取得实例锁；已有实例时提示并返回 false。
        /// 不同 user-data-dir 使用不同的锁，方便并行测试。
        /// </summary>
        public static bool TryAcquire(CommandLineOptions options)
        {
            string userDataDir = AppPaths.ResolveUserDataDir(options == null ? null : options.UserDataDir);
            string name = "Local\\VantageLite-" + Hash(userDataDir);

            bool createdNew;
            try
            {
                _mutex = new Mutex(true, name, out createdNew);
            }
            catch (Exception)
            {
                // 锁不可用时不应阻止启动。
                return true;
            }

            if (createdNew)
            {
                return true;
            }

            _mutex.Dispose();
            _mutex = null;

            MessageBox.Show(
                "Vantage Lite 已在运行。\n\n同一数据目录只允许一个实例，避免重复占用内存。",
                "Vantage Lite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return false;
        }

        private static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()));
                StringBuilder sb = new StringBuilder(12);

                for (int i = 0; i < 6; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
