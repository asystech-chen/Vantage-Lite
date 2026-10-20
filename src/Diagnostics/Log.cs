using System;
using System.IO;
using System.Text;
using VantageLite.App;

namespace VantageLite.Diagnostics
{
    /// <summary>
    /// 轻量日志：线程安全，文件 + 可选控制台。
    /// 依赖：AppPaths。改动注意：日志失败绝不能影响主流程，所有 IO 都要 try/catch。
    /// </summary>
    public static class Log
    {
        private static readonly object Sync = new object();

        private static LogLevel _minimumLevel = LogLevel.Info;
        private static StreamWriter _writer;
        private static bool _consoleOutput;
        private static string _filePath;

        public static string FilePath
        {
            get { return _filePath; }
        }

        public static bool IsEnabled
        {
            get { return _writer != null || _consoleOutput; }
        }

        public static void Initialize(LogLevel minimumLevel, string filePath, bool consoleOutput)
        {
            bool failed = false;

            lock (Sync)
            {
                _minimumLevel = minimumLevel;
                _consoleOutput = consoleOutput;
                _filePath = filePath;

                if (_writer != null)
                {
                    try { _writer.Dispose(); } catch { }
                    _writer = null;
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        AppPaths.EnsureDirectoryForFile(filePath);
                        _writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
                        _writer.AutoFlush = true;
                    }
                    catch
                    {
                        _writer = null;
                        failed = true;
                    }
                }
            }

            if (failed)
            {
                Warn("日志文件创建失败: " + filePath);
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                Info("Log file: " + filePath);
            }
        }

        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message, null);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message, null);
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Warn, message, null);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// 安全地把异常转成文本：某些异常（如 WebView2 初始化失败）的 ToString 本身会抛异常。
        /// 日志与弹窗只能用它，绝不能直接拼接 exception。
        /// </summary>
        public static string Describe(Exception exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            try
            {
                return exception.ToString();
            }
            catch
            {
            }

            string type = null;

            try
            {
                type = exception.GetType().FullName;
            }
            catch
            {
            }

            try
            {
                return (type ?? "Exception") + ": " + exception.Message;
            }
            catch
            {
            }

            return type ?? "Exception (ToString failed)";
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (_writer != null)
                {
                    try { _writer.Flush(); } catch { }
                    try { _writer.Dispose(); } catch { }
                    _writer = null;
                }
            }
        }

        private static void Write(LogLevel level, string message, Exception exception)
        {
            if (level < _minimumLevel)
            {
                return;
            }

            string line;

            try
            {
                line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " [" + LevelText(level) + "] "
                    + (message ?? string.Empty);

                if (exception != null)
                {
                    line += Environment.NewLine + Describe(exception);
                }
            }
            catch
            {
                // 极少数异常在取 Message/ToString 时也会失败，降级为固定文本。
                line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " [" + LevelText(level) + "] "
                    + (message ?? string.Empty)
                    + Environment.NewLine
                    + "Exception (format failed)";
            }

            lock (Sync)
            {
                if (_consoleOutput)
                {
                    try { Console.WriteLine(line); } catch { }
                }

                if (_writer != null)
                {
                    try
                    {
                        _writer.WriteLine(line);
                    }
                    catch
                    {
                        // 写盘失败时静默降级，下一次写仍会重试。
                    }
                }
            }
        }

        private static string LevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info: return "INFO ";
                case LogLevel.Warn: return "WARN ";
                case LogLevel.Error: return "ERROR";
                default: return "TRACE";
            }
        }
    }
}
