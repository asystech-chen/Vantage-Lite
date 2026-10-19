using System;
using System.Collections.Generic;
using VantageLite.Diagnostics;

namespace VantageLite.App
{
    /// <summary>
    /// 命令行参数解析。只负责把 args 变成强类型对象，不产生副作用。
    /// 依赖：Diagnostics.LogLevel。改动注意：新增开关时同步更新 --help 文案。
    /// </summary>
    public sealed class CommandLineOptions
    {
        public bool EnableLog { get; set; }
        public LogLevel LogLevel { get; set; }
        public string LogFile { get; set; }
        public bool ConsoleOutput { get; set; }
        public string UserDataDir { get; set; }
        public string StartUrl { get; set; }
        public bool EnableDevTools { get; set; }
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public List<string> Warnings { get; private set; }

        public CommandLineOptions()
        {
            LogLevel = LogLevel.Info;
            EnableDevTools = true;
            Warnings = new List<string>();
        }

        public static CommandLineOptions Parse(string[] args)
        {
            CommandLineOptions options = new CommandLineOptions();
            if (args == null) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string lower = arg.ToLowerInvariant();
                string value;

                if (lower == "--log" || lower == "-log")
                {
                    options.EnableLog = true;
                    continue;
                }

                if (lower == "--console")
                {
                    options.ConsoleOutput = true;
                    continue;
                }

                if (lower == "--help" || lower == "-h" || lower == "/?")
                {
                    options.ShowHelp = true;
                    continue;
                }

                if (lower == "--version" || lower == "-v")
                {
                    options.ShowVersion = true;
                    continue;
                }

                if (lower == "--no-devtools")
                {
                    options.EnableDevTools = false;
                    continue;
                }

                if (TryReadValue(args, ref i, lower, "--log-level", out value))
                {
                    LogLevel level;
                    if (Enum.TryParse<LogLevel>(value, true, out level))
                    {
                        options.LogLevel = level;
                    }
                    else
                    {
                        options.Warnings.Add("未知日志级别: " + value);
                    }
                    continue;
                }

                if (TryReadValue(args, ref i, lower, "--log-file", out value))
                {
                    options.LogFile = value;
                    continue;
                }

                if (TryReadValue(args, ref i, lower, "--user-data-dir", out value))
                {
                    options.UserDataDir = value;
                    continue;
                }

                if (TryReadValue(args, ref i, lower, "--url", out value))
                {
                    options.StartUrl = value;
                    continue;
                }

                if (arg.StartsWith("-", StringComparison.Ordinal))
                {
                    options.Warnings.Add("未知参数: " + arg);
                    continue;
                }

                if (string.IsNullOrEmpty(options.StartUrl))
                {
                    options.StartUrl = arg;
                }
            }

            return options;
        }

        /// <summary>
        /// 支持 "--name=value" 和 "--name value" 两种写法。
        /// </summary>
        private static bool TryReadValue(string[] args, ref int index, string lowerArg, string name, out string value)
        {
            value = null;
            string prefix = name + "=";

            if (lowerArg.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = args[index].Substring(name.Length + 1);
                return true;
            }

            if (lowerArg == name && index + 1 < args.Length)
            {
                index++;
                value = args[index];
                return true;
            }

            return false;
        }
    }
}
