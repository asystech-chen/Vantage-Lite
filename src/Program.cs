using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Theming;
using VantageLite.UI;

namespace VantageLite
{
    /// <summary>
    /// 程序入口：先安装单文件程序集解析器，再装配日志、主题和主窗口。
    /// 依赖：App / Browser.Bootstrap / Diagnostics / UI。
    /// 改动注意：Main 的第一条语句必须是 EmbeddedAssemblies.Initialize。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 不能在任何 WebView2 类型被 JIT 之前引用，否则程序集会提前加载失败。
            EmbeddedAssemblies.Initialize();
            Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
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

            // 同一数据目录只允许一个实例，避免 WebView2 进程组翻倍。
            if (!SingleInstance.TryAcquire(options))
            {
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

            // 单文件模式：先指定 Loader 路径，再调用任何 WebView2 静态 API。
            WebView2Bootstrap.ConfigureLoader();
            DiagnosticsInfo.CaptureBaseline();
            Log.Info(DiagnosticsInfo.Collect());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                if (!RunStartupChecks())
                {
                    return;
                }

                Application.Run(new MainForm(options));
            }
            catch (Exception ex)
            {
                Log.Error("Fatal error in message loop", ex);
                MessageBox.Show(
                    "程序发生致命错误：\n" + Log.Describe(ex),
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

        /// <summary>
        /// 启动前检查：WebView2 缺失时引导安装并退出；本目录首次启动时提示便携版。
        /// 返回 false 表示程序应直接退出。
        /// </summary>
        private static bool RunStartupChecks()
        {
            if (!WebViewEnvironment.IsRuntimeAvailable)
            {
                bool registered = StartupNotice.IsRuntimeRegistered;

                Log.Warn("未检测到可用的 WebView2 Runtime（注册信息残留=" + registered + "）");

                if (!string.IsNullOrEmpty(WebViewEnvironment.DetectionError))
                {
                    Log.Warn("WebView2 检测异常：" + WebViewEnvironment.DetectionError);
                }

                ThemeManager.Initialize();
                ThemeManager.SetMode("system");

                if (StartupDialogs.ShowWebViewMissing(registered))
                {
                    if (!StartupNotice.OpenWebViewDownloadPage())
                    {
                        ManualDownloadDialog.Show(StartupNotice.WebViewDownloadUrl);
                    }
                }

                return false;
            }

            if (StartupNotice.IsFirstRunInDirectory)
            {
                Log.Info("首次在本目录启动，显示便携版提示");
                ThemeManager.Initialize();
                ThemeManager.SetMode("system");

                if (!StartupDialogs.ShowPortableNotice())
                {
                    Log.Info("用户选择先搬动目录，退出程序");
                    return false;
                }
            }

            return true;
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
            Console.WriteLine("  --log                       启用文件日志");
            Console.WriteLine("  --log-level=LEVEL           debug | info | warn | error | off（默认 info）");
            Console.WriteLine("  --log-file=PATH             指定日志文件路径");
            Console.WriteLine("  --console                   日志同时输出到控制台");
            Console.WriteLine("  --user-data-dir=PATH        指定用户数据目录");
            Console.WriteLine("  --max-live-tabs=N           最多保留的 WebView2 实例数，默认 8（1-50）");
            Console.WriteLine("  --suspend-after-seconds=N   后台标签静默多少秒后挂起，默认 30（0=不挂起）");
            Console.WriteLine("  --discard-after-minutes=N   挂起多少分钟后销毁，默认 60（0=不销毁）");
            Console.WriteLine("  --url=URL                   启动页（也可直接传 URL）");
            Console.WriteLine("  --no-devtools               禁用 F12 开发者工具");
            Console.WriteLine("  --version                   显示版本");
            Console.WriteLine("  --help                      显示本帮助");
        }
    }
}
