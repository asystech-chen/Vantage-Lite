using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VantageLite.Storage
{
    /// <summary>
    /// 书签树节点：文件夹或书签。依赖：无。
    /// 改动注意：Type 决定语义，Children 只在文件夹上有意义；JSON 键名即字段名。
    /// </summary>
    [DataContract]
    public sealed class BookmarkNode
    {
        public const string FolderType = "folder";
        public const string BookmarkType = "bookmark";

        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; }

        [DataMember(Name = "type", Order = 1)]
        public string Type { get; set; }

        [DataMember(Name = "title", Order = 2)]
        public string Title { get; set; }

        [DataMember(Name = "url", Order = 3, EmitDefaultValue = false)]
        public string Url { get; set; }

        [DataMember(Name = "created", Order = 4)]
        public long CreatedUtc { get; set; }

        [DataMember(Name = "children", Order = 5, EmitDefaultValue = false)]
        public List<BookmarkNode> Children { get; set; }

        /// <summary>
        /// 仅导入时使用：toolbar / other / other-folder，不写入 JSON。
        /// </summary>
        public string ImportTarget { get; set; }

        public bool IsFolder
        {
            get { return string.Equals(Type, FolderType, StringComparison.OrdinalIgnoreCase); }
        }

        public IList<BookmarkNode> GetChildren()
        {
            if (Children == null)
            {
                Children = new List<BookmarkNode>();
            }

            return Children;
        }
    }
}
