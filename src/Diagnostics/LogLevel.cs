namespace VantageLite.Diagnostics
{
    /// <summary>
    /// 日志级别。数值越大越严重；Off 关闭全部输出。
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Off = 4
    }
}
