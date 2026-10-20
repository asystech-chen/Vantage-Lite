using System.Collections.Generic;
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

        [DataMember(Name = "suspendSeconds", Order = 9, EmitDefaultValue = false)]
        public int? SuspendSeconds { get; set; }

        [DataMember(Name = "discardMinutes", Order = 10, EmitDefaultValue = false)]
        public int? DiscardMinutes { get; set; }

        [DataMember(Name = "search", Order = 11, EmitDefaultValue = false)]
        public string SearchEngine { get; set; }

        [DataMember(Name = "newTabMode", Order = 12, EmitDefaultValue = false)]
        public string NewTabMode { get; set; }

        [DataMember(Name = "newTabUrl", Order = 13, EmitDefaultValue = false)]
        public string NewTabUrl { get; set; }

        [DataMember(Name = "newTabShortcuts", Order = 14, EmitDefaultValue = false)]
        public bool? NewTabShortcuts { get; set; }

        [DataMember(Name = "address", Order = 15, EmitDefaultValue = false)]
        public string Address { get; set; }

        [DataMember(Name = "theme", Order = 16, EmitDefaultValue = false)]
        public string ThemeMode { get; set; }

        [DataMember(Name = "clearOnExit", Order = 17, EmitDefaultValue = false)]
        public bool? ClearOnExit { get; set; }

        [DataMember(Name = "homeMode", Order = 18, EmitDefaultValue = false)]
        public string HomeMode { get; set; }

        [DataMember(Name = "customEngines", Order = 20, EmitDefaultValue = false)]
        public List<CustomSearchEngine> CustomSearchEngines { get; set; }

        [DataMember(Name = "downloadDir", Order = 21, EmitDefaultValue = false)]
        public string DownloadDirectory { get; set; }

        [DataMember(Name = "restoreSession", Order = 22, EmitDefaultValue = false)]
        public bool? RestoreSession { get; set; }
    }
}
