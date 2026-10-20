using System.Runtime.Serialization;

namespace VantageLite.Storage
{
    /// <summary>
    /// 单条书签。依赖：无。改动注意：字段名即 JSON 键，改名会丢失旧数据。
    /// </summary>
    [DataContract]
    public sealed class Bookmark
    {
        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; }

        [DataMember(Name = "title", Order = 1)]
        public string Title { get; set; }

        [DataMember(Name = "url", Order = 2)]
        public string Url { get; set; }

        [DataMember(Name = "created", Order = 3)]
        public long CreatedUtc { get; set; }
    }
}
