using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VantageLite.App
{
    /// <summary>
    /// 让 WinExe 也能把 --help / --console 输出到调用它的控制台。
    /// 依赖：kernel32。改动注意：只在确实需要控制台时调用 Attach()。
    /// </summary>
    public static class ConsoleHelper
    {
        private const int AttachParentProcess = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        public static void Attach()
        {
            if (!AttachConsole(AttachParentProcess))
            {
                AllocConsole();
            }

            try
            {
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput());
                writer.AutoFlush = true;
                Console.SetOut(writer);
                Console.SetError(writer);
            }
            catch
            {
                // 控制台不可用时静默失败，不影响主程序。
            }
        }
    }
}
