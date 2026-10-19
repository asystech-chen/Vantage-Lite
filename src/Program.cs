using System;
using System.Text;
using System.Windows.Forms;
using VantageLite.App;
using VantageLite.Diagnostics;
using VantageLite.UI;

namespace VantageLite
{
    /// <summary>
    /// 程序入口：解析命令行、初始化日志、安装异常保护、启动主窗口。
    /// 依赖：App / Diagnostics / UI。改动注意：保持这里只做装配，不写业务逻辑。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            CommandLineOptions options = CommandLineOptions.Parse(args);

            if (options.ShowHelp || options.ShowVersion || options.ConsoleOutput)
            {
                ConsoleHelper.Attach();
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            }

            if (options.ShowHelp)
            {
                PrintHelp();
                return;
            }

            if (options.ShowVersion)
            {
                Console.WriteLine(GetVersionText());
                return;
            }

            string logFile = options.EnableLog ? AppPaths.ResolveLogFile(options.LogFile) : null;
            Log.Initialize(options.LogLevel, logFile, options.ConsoleOutput);
            CrashGuard.Install();

            Log.Info("=== Vantage Lite " + Application.ProductVersion + " ===");
            Log.Info("Command line: " + Environment.CommandLine);
            for (int i = 0; i < options.Warnings.Count; i++)
            {
                Log.Warn(options.Warnings[i]);
            }
            Log.Info(DiagnosticsInfo.Collect());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm(options));
            }
            catch (Exception ex)
            {
                Log.Error("Fatal error in message loop", ex);
                MessageBox.Show(
                    "程序发生致命错误：\n" + ex.Message,
                    "Vantage Lite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Log.Info("Vantage Lite exiting.");
                Log.Shutdown();
            }
        }

        private static string GetVersionText()
        {
            return "Vantage Lite " + Application.ProductVersion;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(GetVersionText());
            Console.WriteLine();
            Console.WriteLine("用法: VantageLite.exe [选项] [URL]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  --log                 启用文件日志");
            Console.WriteLine("  --log-level=LEVEL     debug | info | warn | error | off（默认 info）");
            Console.WriteLine("  --log-file=PATH       指定日志文件路径");
            Console.WriteLine("  --console             日志同时输出到控制台");
            Console.WriteLine("  --user-data-dir=PATH  指定用户数据目录（独立 profile）");
            Console.WriteLine("  --max-live-tabs=N     同时存活的标签上限，默认 5（范围 1-50）");
            Console.WriteLine("  --url=URL             启动页（也可直接传 URL）");
            Console.WriteLine("  --no-devtools         禁用 F12 开发者工具");
            Console.WriteLine("  --version             显示版本");
            Console.WriteLine("  --help                显示本帮助");
        }
    }
}
