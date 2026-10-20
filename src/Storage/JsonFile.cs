using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using VantageLite.App;
using VantageLite.Diagnostics;

namespace VantageLite.Storage
{
    /// <summary>
    /// 用户设置。依赖：无。改动注意：字段名即 JSON 键。
    /// </summary>
    /// <summary>
    /// 用户自定义搜索引擎。Url 中用 {query} 表示搜索词占位符。
    /// </summary>
    [DataContract]
    public sealed class CustomSearchEngine
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; }

        [DataMember(Name = "url", Order = 1)]
        public string Url { get; set; }
    }

    [DataContract]
    public sealed class AppSettings
    {
        public const string DefaultHomeUrl = "https://asystech.cn/start/index.html";
        public const int DefaultMaxLiveTabs = 8;
        public const int DefaultSuspendSeconds = 30;
        public const int DefaultDiscardMinutes = 60;
        public const string DefaultSearchEngine = "bing";
        public const string DefaultNewTabMode = "builtin";
        public const string DefaultThemeMode = "system";
        public const string DefaultHomeMode = "asys";

        [DataMember(Name = "home", Order = 0)]
        public string HomeUrl { get; set; }

        [DataMember(Name = "maxTabs", Order = 1)]
        public int MaxLiveTabs { get; set; }

        [DataMember(Name = "bookmarksBar", Order = 2)]
        public bool ShowBookmarksBar { get; set; }

        [DataMember(Name = "suspendSeconds", Order = 3)]
        public int SuspendAfterSeconds { get; set; }

        [DataMember(Name = "discardMinutes", Order = 4)]
        public int DiscardAfterMinutes { get; set; }

        [DataMember(Name = "search", Order = 5)]
        public string SearchEngine { get; set; }

        [DataMember(Name = "newTabMode", Order = 6)]
        public string NewTabMode { get; set; }

        [DataMember(Name = "newTabUrl", Order = 7)]
        public string NewTabUrl { get; set; }

        [DataMember(Name = "newTabShortcuts", Order = 8)]
        public bool NewTabShortcuts { get; set; }

        [DataMember(Name = "theme", Order = 9)]
        public string ThemeMode { get; set; }

        [DataMember(Name = "clearOnExit", Order = 10)]
        public bool ClearOnExit { get; set; }

        [DataMember(Name = "homeMode", Order = 11)]
        public string HomeMode { get; set; }

        [DataMember(Name = "customEngines", Order = 13)]
        public List<CustomSearchEngine> CustomSearchEngines { get; set; }

        [DataMember(Name = "downloadDir", Order = 14)]
        public string DownloadDirectory { get; set; }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            // 旧配置文件没有这些字段时，先填默认值，避免被读成 0。
            SuspendAfterSeconds = DefaultSuspendSeconds;
            DiscardAfterMinutes = DefaultDiscardMinutes;
            SearchEngine = DefaultSearchEngine;
            NewTabMode = DefaultNewTabMode;
            NewTabUrl = string.Empty;
            NewTabShortcuts = false;
            ThemeMode = DefaultThemeMode;
            ClearOnExit = false;
            CustomSearchEngines = new List<CustomSearchEngine>();
            DownloadDirectory = string.Empty;
            // HomeMode 故意不填：旧配置由 MainForm 按 HomeUrl 推断。
        }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                HomeUrl = DefaultHomeUrl,
                MaxLiveTabs = DefaultMaxLiveTabs,
                ShowBookmarksBar = true,
                SuspendAfterSeconds = DefaultSuspendSeconds,
                DiscardAfterMinutes = DefaultDiscardMinutes,
                SearchEngine = DefaultSearchEngine,
                NewTabMode = DefaultNewTabMode,
                NewTabUrl = string.Empty,
                NewTabShortcuts = false,
                ThemeMode = DefaultThemeMode,
                ClearOnExit = false,
                HomeMode = DefaultHomeMode,
                CustomSearchEngines = new List<CustomSearchEngine>(),
                DownloadDirectory = string.Empty
            };
        }
    }

    /// <summary>
    /// 极简 JSON 读写：原子写入（临时文件 + 替换）。
    /// 依赖：AppPaths / Log。改动注意：读取失败一律回退到默认值，不能让数据文件损坏阻断启动。
    /// </summary>
    internal static class JsonFile
    {
        public static T Load<T>(string path, T fallback) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return fallback;
                }

                using (FileStream stream = File.OpenRead(path))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return serializer.ReadObject(stream) as T ?? fallback;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("读取 " + path + " 失败：" + ex.Message);
                return fallback;
            }
        }

        public static T Parse<T>(string json) where T : class
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return serializer.ReadObject(stream) as T;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void Save<T>(string path, T value) where T : class
        {
            try
            {
                AppPaths.EnsureDirectoryForFile(path);

                string temp = path + ".tmp";
                using (FileStream stream = File.Create(temp))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                }

                if (File.Exists(path))
                {
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
                else
                {
                    File.Move(temp, path);
                }

                Log.Debug("已保存 " + path);
            }
            catch (Exception ex)
            {
                Log.Warn("写入 " + path + " 失败：" + ex.Message);
            }
        }
    }
}
