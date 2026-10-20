using System.Runtime.Serialization;

namespace VantageLite.Storage
{
    /// <summary>
    /// 书签页通过 web message 发回宿主的最小命令对象。
    /// 依赖：无。改动注意：字段名对应 JS 里的 JSON 键。
    /// </summary>
    [DataContract]
    public sealed class BookmarkCommand
    {
        [DataMember(Name = "cmd", Order = 0)]
        public string Cmd { get; set; }

        [DataMember(Name = "id", Order = 1, EmitDefaultValue = false)]
        public string Id { get; set; }

        [DataMember(Name = "parent", Order = 2, EmitDefaultValue = false)]
        public string ParentId { get; set; }

        [DataMember(Name = "name", Order = 3, EmitDefaultValue = false)]
        public string Name { get; set; }

        [DataMember(Name = "before", Order = 4, EmitDefaultValue = false)]
        public string Before { get; set; }

        [DataMember(Name = "home", Order = 5, EmitDefaultValue = false)]
        public string Home { get; set; }

        [DataMember(Name = "maxTabs", Order = 6, EmitDefaultValue = false)]
        public int MaxTabs { get; set; }

        [DataMember(Name = "bookmarksBar", Order = 7, EmitDefaultValue = false)]
        public bool BookmarksBar { get; set; }

        [DataMember(Name = "open", Order = 8, EmitDefaultValue = false)]
        public bool Open { get; set; }
    }
}
