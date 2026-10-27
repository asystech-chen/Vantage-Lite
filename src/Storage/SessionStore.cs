using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using VantageLite.Diagnostics;

namespace VantageLite.Storage
{
    /// <summary>
    /// 单个待恢复标签。依赖：无。
    /// </summary>
    [DataContract]
    public sealed class SessionTab
    {
        [DataMember(Name = "url", Order = 0)]
        public string Url { get; set; }

        [DataMember(Name = "title", Order = 1)]
        public string Title { get; set; }

        [DataMember(Name = "pinned", Order = 2)]
        public bool Pinned { get; set; }
    }

    /// <summary>
    /// 上次退出时的标签集合。依赖：无。
    /// </summary>
    [DataContract]
    public sealed class SessionData
    {
        [DataMember(Name = "tabs", Order = 0)]
        public List<SessionTab> Tabs { get; set; }

        [DataMember(Name = "active", Order = 1)]
        public int ActiveIndex { get; set; }
    }

    /// <summary>
    /// 会话文件读写。依赖：JsonFile / Log。
    /// 改动注意：关闭时清空隐私数据的情况下必须删除会话文件，不能留下恢复入口。
    /// </summary>
    internal static class SessionStore
    {
        public static SessionData Load(string path)
        {
            return JsonFile.Load<SessionData>(path, null);
        }

        public static void Save(string path, SessionData data)
        {
            JsonFile.Save(path, data);
        }

        public static void Clear(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("删除会话文件失败：" + Log.Describe(ex));
            }
        }
    }
}
