using System;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Web.WebView2.Core;

namespace VantageLite.Downloads
{
    /// <summary>
    /// 一条下载记录。持久化字段之外，Operation/Paused/Updated 只在运行时使用。
    /// 依赖：WebView2 DownloadOperation。改动注意：State 用固定小写字符串，和 JSON 兼容。
    /// </summary>
    [DataContract]
    public sealed class DownloadItem
    {
        public const string StateProgress = "progress";
        public const string StatePaused = "paused";
        public const string StateCompleted = "completed";
        public const string StateInterrupted = "interrupted";
        public const string StateCancelled = "cancelled";

        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; }

        [DataMember(Name = "url", Order = 1)]
        public string Url { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string FilePath { get; set; }

        [DataMember(Name = "name", Order = 3)]
        public string FileName { get; set; }

        [DataMember(Name = "received", Order = 4)]
        public long BytesReceived { get; set; }

        [DataMember(Name = "total", Order = 5)]
        public long TotalBytes { get; set; }

        [DataMember(Name = "state", Order = 6)]
        public string State { get; set; }

        [DataMember(Name = "error", Order = 7)]
        public string Error { get; set; }

        [DataMember(Name = "started", Order = 8)]
        public DateTime StartedUtc { get; set; }

        [IgnoreDataMember]
        public CoreWebView2DownloadOperation Operation { get; set; }

        [IgnoreDataMember]
        public bool Paused { get; set; }

        /// <summary>
        /// 是否属于本次会话（弹出面板只显示这些；管理页显示全部）。不落盘。
        /// </summary>
        [IgnoreDataMember]
        public bool IsSession { get; set; }

        public event EventHandler Updated;

        public bool IsActive
        {
            get
            {
                return string.Equals(State, StateProgress, StringComparison.Ordinal)
                    || string.Equals(State, StatePaused, StringComparison.Ordinal);
            }
        }

        public bool IsCompleted
        {
            get { return string.Equals(State, StateCompleted, StringComparison.Ordinal); }
        }

        public bool IsFailed
        {
            get
            {
                return string.Equals(State, StateInterrupted, StringComparison.Ordinal)
                    || string.Equals(State, StateCancelled, StringComparison.Ordinal);
            }
        }

        public double Progress
        {
            get
            {
                if (TotalBytes <= 0)
                {
                    return 0;
                }

                return Math.Max(0, Math.Min(1.0, (double)BytesReceived / TotalBytes));
            }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(FileName))
                {
                    return FileName;
                }

                if (!string.IsNullOrEmpty(FilePath))
                {
                    return Path.GetFileName(FilePath);
                }

                return "下载文件";
            }
        }

        public void RaiseUpdated()
        {
            EventHandler handler = Updated;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
