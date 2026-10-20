using System;
using System.Threading;
using System.Windows.Forms;
using VantageLite.Diagnostics;

namespace VantageLite.App
{
    /// <summary>
    /// 全局异常兜底：界面线程异常和未处理异常都写日志并弹窗。
    /// 依赖：Log。改动注意：这里不能再抛异常，所有路径都要 try/catch。
    /// </summary>
    public static class CrashGuard
    {
        public static void Install()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Report("界面线程异常", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Report("未处理异常", e.ExceptionObject as Exception);
        }

        private static void Report(string title, Exception ex)
        {
            try
            {
                Log.Error(title, ex);
            }
            catch
            {
            }

            string logHint = Log.FilePath;
            if (string.IsNullOrEmpty(logHint))
            {
                logHint = "未开启日志（使用 --log 启动可记录）";
            }

            try
            {
                string message = ex == null ? title : title + "：\n" + VantageLite.Diagnostics.Log.Describe(ex);
                MessageBox.Show(
                    message + "\n\n日志：" + logHint,
                    "Vantage Lite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
