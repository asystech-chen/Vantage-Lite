using System.Runtime.Serialization;

namespace VantageLite.Storage
{
    /// <summary>
    /// 单条浏览历史。依赖：无。改动注意：字段名即 JSON 键。
    /// </summary>
    [DataContract]
    public sealed class HistoryEntry
    {
        [DataMember(Name = "url", Order = 0)]
        public string Url { get; set; }

        [DataMember(Name = "title", Order = 1)]
        public string Title { get; set; }

        [DataMember(Name = "visited", Order = 2)]
        public long VisitedUtc { get; set; }
    }
}
