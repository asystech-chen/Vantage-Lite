using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using VantageLite.Diagnostics;

namespace VantageLite.Storage
{
    /// <summary>
    /// 已安装浏览器书签来源探测：Chromium 系 Bookmarks JSON 与 Firefox 自动备份（mozLz4）。
    /// 依赖：BookmarkNode / Log。改动注意：路径不存在一律跳过；所有读取都允许共享写。
    /// </summary>
    public static class BookmarkSources
    {
        private static readonly DataContractJsonSerializerSettings JsonSettings =
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };

        public sealed class Source
        {
            public string Browser { get; set; }

            public string Profile { get; set; }

            public string Kind { get; set; }

            public string Path { get; set; }

            public int Count { get; set; }

            public override string ToString()
            {
                string profile = string.IsNullOrWhiteSpace(Profile) ? "默认" : Profile;
                string count = Count >= 0 ? "（" + Count + " 条）" : string.Empty;
                return Browser + " — " + profile + count;
            }
        }

        public sealed class ScanResult
        {
            public List<Source> Sources { get; private set; } = new List<Source>();

            public List<string> Notes { get; private set; } = new List<string>();
        }

        /// <summary>
        /// 探测当前用户下已安装且有书签的浏览器配置。
        /// </summary>
        public static ScanResult Detect()
        {
            ScanResult result = new ScanResult();

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            AddChromium(result, "Chrome", Path.Combine(local, "Google", "Chrome", "User Data"));
            AddChromium(result, "Edge", Path.Combine(local, "Microsoft", "Edge", "User Data"));
            AddChromium(result, "Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"));
            AddChromium(result, "Vivaldi", Path.Combine(local, "Vivaldi", "User Data"));
            AddChromium(result, "Chromium", Path.Combine(local, "Chromium", "User Data"));
            AddChromium(result, "Yandex", Path.Combine(local, "Yandex", "YandexBrowser", "User Data"));

            // QQ 浏览器新旧安装器用的根目录不同，优先 Tencent 路径。
            if (AddChromium(result, "QQ浏览器", Path.Combine(local, "Tencent", "QQBrowser", "User Data")) == 0)
            {
                AddChromium(result, "QQ浏览器", Path.Combine(local, "QQBrowser", "User Data"));
            }

            AddChromium(result, "360极速", Path.Combine(local, "360Chrome", "Chrome", "User Data"));
            AddChromium(result, "360极速X", Path.Combine(local, "360ChromeX", "Chrome", "User Data"));

            AddOpera(result, "Opera", Path.Combine(roaming, "Opera Software", "Opera Stable"));
            AddOpera(result, "Opera GX", Path.Combine(roaming, "Opera Software", "Opera GX Stable"));

            AddFirefox(result, "Firefox", Path.Combine(roaming, "Mozilla", "Firefox"));
            AddFirefox(result, "Vantage", Path.Combine(roaming, "vantage"));
            AddFirefox(result, "LibreWolf", Path.Combine(roaming, "librewolf"));
            AddFirefox(result, "Waterfox", Path.Combine(roaming, "Waterfox"));

            return result;
        }

        /// <summary>
        /// 把来源读取成书签树；失败返回 null。
        /// </summary>
        public static BookmarkNode Load(Source source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Path))
            {
                return null;
            }

            if (string.Equals(source.Kind, "firefox", StringComparison.OrdinalIgnoreCase))
            {
                return LoadFirefox(source);
            }

            return LoadChromium(source);
        }

        private static int AddChromium(ScanResult result, string browser, string userData)
        {
            if (!Directory.Exists(userData))
            {
                return 0;
            }

            int added = 0;

            Dictionary<string, string> names = ReadProfileNames(userData);
            List<string> directories = new List<string>(Directory.GetDirectories(userData));
            directories.Sort(CompareProfileDirectories);

            for (int i = 0; i < directories.Count; i++)
            {
                string directory = directories[i];
                List<string> files = FindBookmarkFiles(directory);

                for (int j = 0; j < files.Count; j++)
                {
                    string file = files[j];

                    try
                    {
                        BookmarkNode tree = ReadChromium(file);

                        if (tree == null)
                        {
                            continue;
                        }

                        int count = CountBookmarks(tree);

                        if (count == 0)
                        {
                            continue;
                        }

                        string directoryName = Path.GetFileName(directory);
                        string profile;

                        if (!names.TryGetValue(directoryName, out profile) ||
                            string.IsNullOrWhiteSpace(profile))
                        {
                            profile = string.Equals(directoryName, "Default", StringComparison.OrdinalIgnoreCase)
                                ? "默认"
                                : directoryName;
                        }

                        string fileDirectory = Path.GetDirectoryName(file);

                        if (!string.Equals(fileDirectory, directory, StringComparison.OrdinalIgnoreCase))
                        {
                            profile += " / " + Path.GetFileName(fileDirectory);
                        }

                        result.Sources.Add(new Source
                        {
                            Browser = browser,
                            Profile = profile,
                            Kind = "chromium",
                            Path = file,
                            Count = count
                        });

                        added++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("读取 " + browser + " 书签失败：" + ex.Message);
                        result.Notes.Add(browser + " 书签读取失败：" + Path.GetFileName(directory));
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// 常见浏览器书签文件名；360 新版会在 profile 下再套一层账号目录。
        /// </summary>
        private static List<string> FindBookmarkFiles(string directory)
        {
            List<string> files = new List<string>();

            AddIfExists(files, Path.Combine(directory, "Bookmarks"));
            AddIfExists(files, Path.Combine(directory, "360Bookmarks"));

            if (files.Count > 0)
            {
                return files;
            }

            string[] subdirectories = Directory.GetDirectories(directory);
            Array.Sort(subdirectories, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < subdirectories.Length; i++)
            {
                AddIfExists(files, Path.Combine(subdirectories[i], "Bookmarks"));
                AddIfExists(files, Path.Combine(subdirectories[i], "360Bookmarks"));
            }

            return files;
        }

        private static void AddIfExists(List<string> files, string path)
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        private static void AddOpera(ScanResult result, string browser, string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            string file = Path.Combine(directory, "Bookmarks");

            if (!File.Exists(file))
            {
                return;
            }

            try
            {
                BookmarkNode tree = ReadChromium(file);

                if (tree == null)
                {
                    return;
                }

                int count = CountBookmarks(tree);

                if (count == 0)
                {
                    return;
                }

                result.Sources.Add(new Source
                {
                    Browser = browser,
                    Profile = "默认",
                    Kind = "chromium",
                    Path = file,
                    Count = count
                });

            }
            catch (Exception ex)
            {
                Log.Warn("读取 " + browser + " 书签失败：" + ex.Message);
                result.Notes.Add(browser + " 书签读取失败");
            }
        }

        private static void AddFirefox(ScanResult result, string browser, string firefoxRoot)
        {
            if (!Directory.Exists(firefoxRoot))
            {
                return;
            }

            List<FirefoxProfile> profiles = ReadFirefoxProfiles(firefoxRoot);

            if (profiles.Count == 0)
            {
                result.Notes.Add("检测到 " + browser + "，但找不到 profiles.ini");
                return;
            }

            bool found = false;

            for (int i = 0; i < profiles.Count; i++)
            {
                FirefoxProfile profile = profiles[i];

                if (string.IsNullOrWhiteSpace(profile.Path))
                {
                    continue;
                }

                string directory = profile.IsRelative
                    ? Path.Combine(firefoxRoot, profile.Path.Replace('/', Path.DirectorySeparatorChar))
                    : profile.Path;

                if (!Directory.Exists(directory))
                {
                    continue;
                }

                string backups = Path.Combine(directory, "bookmarkbackups");

                if (!Directory.Exists(backups))
                {
                    continue;
                }

                FileInfo newest = NewestBackup(backups);

                if (newest == null)
                {
                    continue;
                }

                try
                {
                    BookmarkNode tree = ReadFirefoxBackup(newest.FullName);

                    if (tree == null)
                    {
                        continue;
                    }

                    int count = CountBookmarks(tree);

                    if (count == 0)
                    {
                        continue;
                    }

                    result.Sources.Add(new Source
                    {
                        Browser = browser,
                        Profile = string.IsNullOrWhiteSpace(profile.Name) ? "默认" : profile.Name,
                        Kind = "firefox",
                        Path = newest.FullName,
                        Count = count
                    });

                    found = true;
                }
                catch (Exception ex)
                {
                    Log.Warn("读取 " + browser + " 书签备份失败：" + ex.Message);
                    result.Notes.Add(browser + " 备份读取失败：" + profile.Name);
                }
            }

            if (!found)
            {
                result.Notes.Add(browser + " 没有可用的书签备份，可在 " + browser + " 中导出 HTML 后用「从文件…」导入");
            }
        }

        private static BookmarkNode LoadChromium(Source source)
        {
            BookmarkNode tree = ReadChromium(source.Path);

            if (tree == null)
            {
                return null;
            }

            string profile = string.IsNullOrWhiteSpace(source.Profile) ? "默认" : source.Profile;
            tree.Title = source.Browser + "（" + profile + "）";
            return tree;
        }

        private static BookmarkNode LoadFirefox(Source source)
        {
            BookmarkNode tree = ReadFirefoxBackup(source.Path);

            if (tree == null)
            {
                return null;
            }

            string profile = string.IsNullOrWhiteSpace(source.Profile) ? "默认" : source.Profile;
            tree.Title = source.Browser + "（" + profile + "）";
            return tree;
        }

        private static BookmarkNode ReadChromium(string path)
        {
            byte[] bytes = ReadAllBytesShared(path);

            ChromiumFile file;

            using (MemoryStream stream = new MemoryStream(bytes))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(ChromiumFile), JsonSettings);
                file = serializer.ReadObject(stream) as ChromiumFile;
            }

            if (file == null || file.Roots == null)
            {
                return null;
            }

            BookmarkNode tree = NewFolder("Chromium");

            AppendConverted(tree, ConvertChromium(file.Roots.BookmarkBar));
            AppendConverted(tree, ConvertChromium(file.Roots.Other));
            AppendConverted(tree, ConvertChromium(file.Roots.Synced));

            return tree;
        }

        private static void AppendConverted(BookmarkNode parent, BookmarkNode child)
        {
            if (child != null)
            {
                parent.GetChildren().Add(child);
            }
        }

        private static BookmarkNode ConvertChromium(ChromiumNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (string.Equals(node.Type, "url", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(node.Url))
                {
                    return null;
                }

                return NewBookmark(node.Name, node.Url, FromChromiumTime(node.DateAdded));
            }

            if (node.Children == null)
            {
                return null;
            }

            BookmarkNode folder = NewFolder(string.IsNullOrWhiteSpace(node.Name) ? "未命名文件夹" : node.Name);
            folder.CreatedUtc = FromChromiumTime(node.DateAdded);

            for (int i = 0; i < node.Children.Count; i++)
            {
                AppendConverted(folder, ConvertChromium(node.Children[i]));
            }

            return folder;
        }

        private static BookmarkNode ReadFirefoxBackup(string path)
        {
            byte[] compressed = ReadAllBytesShared(path);
            byte[] json = DecompressMozLz4(compressed);

            FirefoxNode root;

            using (MemoryStream stream = new MemoryStream(json))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(FirefoxNode), JsonSettings);
                root = serializer.ReadObject(stream) as FirefoxNode;
            }

            if (root == null)
            {
                return null;
            }

            BookmarkNode tree = NewFolder("Firefox");

            if (root.Children != null)
            {
                for (int i = 0; i < root.Children.Count; i++)
                {
                    AppendConverted(tree, ConvertFirefox(root.Children[i]));
                }
            }

            return tree;
        }

        private static BookmarkNode ConvertFirefox(FirefoxNode node)
        {
            if (node == null)
            {
                return null;
            }

            bool isFolder = node.Children != null ||
                (node.Type != null &&
                 node.Type.EndsWith("container", StringComparison.OrdinalIgnoreCase));

            if (isFolder)
            {
                // 标签（tags）不是用户的文件夹，导入后会变成一堆噪音。
                if (string.Equals(node.Guid, "tags________", StringComparison.Ordinal))
                {
                    return null;
                }

                BookmarkNode folder = NewFolder(string.IsNullOrWhiteSpace(node.Title) ? "未命名文件夹" : node.Title);
                folder.CreatedUtc = FromFirefoxTime(node.DateAdded);

                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        AppendConverted(folder, ConvertFirefox(node.Children[i]));
                    }
                }

                return folder;
            }

            if (string.Equals(node.Type, "text/x-moz-place-separator", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(node.Uri))
            {
                return null;
            }

            return NewBookmark(node.Title, node.Uri, FromFirefoxTime(node.DateAdded));
        }

        private static BookmarkNode NewFolder(string title)
        {
            return new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.FolderType,
                Title = title,
                CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Children = new List<BookmarkNode>()
            };
        }

        private static BookmarkNode NewBookmark(string title, string url, long createdUtc)
        {
            return new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.BookmarkType,
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                Url = url,
                CreatedUtc = createdUtc > 0 ? createdUtc : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private static Dictionary<string, string> ReadProfileNames(string userData)
        {
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string localState = Path.Combine(userData, "Local State");

            if (!File.Exists(localState))
            {
                return names;
            }

            try
            {
                byte[] bytes = ReadAllBytesShared(localState);
                LocalStateFile file;

                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(LocalStateFile), JsonSettings);
                    file = serializer.ReadObject(stream) as LocalStateFile;
                }

                if (file != null && file.Profile != null && file.Profile.InfoCache != null)
                {
                    foreach (KeyValuePair<string, LocalStateInfo> pair in file.Profile.InfoCache)
                    {
                        if (pair.Value != null && !string.IsNullOrWhiteSpace(pair.Value.Name))
                        {
                            names[pair.Key] = pair.Value.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("读取 Local State 失败：" + ex.Message);
            }

            return names;
        }

        private static int CompareProfileDirectories(string left, string right)
        {
            string a = Path.GetFileName(left);
            string b = Path.GetFileName(right);

            int rankA = ProfileRank(a);
            int rankB = ProfileRank(b);

            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int ProfileRank(string name)
        {
            if (string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private static List<FirefoxProfile> ReadFirefoxProfiles(string firefoxRoot)
        {
            List<FirefoxProfile> profiles = new List<FirefoxProfile>();
            string path = Path.Combine(firefoxRoot, "profiles.ini");

            if (!File.Exists(path))
            {
                return profiles;
            }

            FirefoxProfile current = null;

            using (StreamReader reader = new StreamReader(path))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                    {
                        continue;
                    }

                    if (line[0] == '[')
                    {
                        current = line.StartsWith("[Profile", StringComparison.OrdinalIgnoreCase)
                            ? new FirefoxProfile()
                            : null;

                        if (current != null)
                        {
                            profiles.Add(current);
                        }

                        continue;
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    int equals = line.IndexOf('=');

                    if (equals <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();

                    if (string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Name = value;
                    }
                    else if (string.Equals(key, "Path", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Path = value;
                    }
                    else if (string.Equals(key, "IsRelative", StringComparison.OrdinalIgnoreCase))
                    {
                        current.IsRelative = value != "0";
                    }
                }
            }

            return profiles;
        }

        private static FileInfo NewestBackup(string directory)
        {
            FileInfo newest = null;
            string[] files = Directory.GetFiles(directory, "*.jsonlz4");

            for (int i = 0; i < files.Length; i++)
            {
                FileInfo info = new FileInfo(files[i]);

                if (newest == null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                {
                    newest = info;
                }
            }

            return newest;
        }

        private static byte[] ReadAllBytesShared(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (MemoryStream memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static byte[] DecompressMozLz4(byte[] input)
        {
            if (input == null || input.Length < 12 ||
                input[0] != (byte)'m' || input[1] != (byte)'o' || input[2] != (byte)'z' ||
                input[3] != (byte)'L' || input[4] != (byte)'z' || input[5] != (byte)'4' ||
                input[6] != (byte)'0' || input[7] != 0)
            {
                throw new InvalidDataException("不是有效的 mozLz4 文件");
            }

            int outputSize = BitConverter.ToInt32(input, 8);

            if (outputSize < 0 || outputSize > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("mozLz4 解压尺寸异常");
            }

            byte[] output = new byte[outputSize];
            int inputIndex = 12;
            int outputIndex = 0;

            while (inputIndex < input.Length)
            {
                int token = input[inputIndex++];
                int literalLength = token >> 4;

                if (literalLength == 15)
                {
                    int extra;

                    do
                    {
                        if (inputIndex >= input.Length)
                        {
                            throw new InvalidDataException("mozLz4 数据截断");
                        }

                        extra = input[inputIndex++];
                        literalLength += extra;
                    }
                    while (extra == 255);
                }

                if (inputIndex + literalLength > input.Length ||
                    outputIndex + literalLength > output.Length)
                {
                    throw new InvalidDataException("mozLz4 literal 越界");
                }

                Buffer.BlockCopy(input, inputIndex, output, outputIndex, literalLength);
                inputIndex += literalLength;
                outputIndex += literalLength;

                if (inputIndex >= input.Length)
                {
                    break;
                }

                if (inputIndex + 2 > input.Length)
                {
                    throw new InvalidDataException("mozLz4 offset 截断");
                }

                int offset = input[inputIndex] | (input[inputIndex + 1] << 8);
                inputIndex += 2;

                if (offset <= 0 || offset > outputIndex)
                {
                    throw new InvalidDataException("mozLz4 offset 越界");
                }

                int matchLength = (token & 0x0F) + 4;

                if ((token & 0x0F) == 15)
                {
                    int extra;

                    do
                    {
                        if (inputIndex >= input.Length)
                        {
                            throw new InvalidDataException("mozLz4 数据截断");
                        }

                        extra = input[inputIndex++];
                        matchLength += extra;
                    }
                    while (extra == 255);
                }

                if (outputIndex + matchLength > output.Length)
                {
                    throw new InvalidDataException("mozLz4 match 越界");
                }

                int sourceIndex = outputIndex - offset;

                for (int i = 0; i < matchLength; i++)
                {
                    output[outputIndex + i] = output[sourceIndex + i];
                }

                outputIndex += matchLength;
            }

            if (outputIndex != outputSize)
            {
                throw new InvalidDataException("mozLz4 解压长度不符");
            }

            return output;
        }

        private static int CountBookmarks(BookmarkNode node)
        {
            if (node == null)
            {
                return 0;
            }

            int count = node.IsFolder ? 0 : 1;

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    count += CountBookmarks(node.Children[i]);
                }
            }

            return count;
        }

        private static long FromChromiumTime(string value)
        {
            long micros;

            if (string.IsNullOrWhiteSpace(value) || !long.TryParse(value.Trim(), out micros) || micros <= 0)
            {
                return 0;
            }

            try
            {
                DateTime time = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(micros * 10);
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (long)(time - epoch).TotalSeconds;
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0;
            }
        }

        private static long FromFirefoxTime(long micros)
        {
            return micros > 0 ? micros / 1000000 : 0;
        }

        private sealed class FirefoxProfile
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public bool IsRelative { get; set; } = true;
        }

        [DataContract]
        private sealed class ChromiumFile
        {
            [DataMember(Name = "roots")]
            public ChromiumRoots Roots { get; set; }
        }

        [DataContract]
        private sealed class ChromiumRoots
        {
            [DataMember(Name = "bookmark_bar")]
            public ChromiumNode BookmarkBar { get; set; }

            [DataMember(Name = "other")]
            public ChromiumNode Other { get; set; }

            [DataMember(Name = "synced")]
            public ChromiumNode Synced { get; set; }
        }

        [DataContract]
        private sealed class ChromiumNode
        {
            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "url")]
            public string Url { get; set; }

            [DataMember(Name = "date_added")]
            public string DateAdded { get; set; }

            [DataMember(Name = "children")]
            public List<ChromiumNode> Children { get; set; }
        }

        [DataContract]
        private sealed class FirefoxNode
        {
            [DataMember(Name = "guid")]
            public string Guid { get; set; }

            [DataMember(Name = "title")]
            public string Title { get; set; }

            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "uri")]
            public string Uri { get; set; }

            [DataMember(Name = "dateAdded")]
            public long DateAdded { get; set; }

            [DataMember(Name = "children")]
            public List<FirefoxNode> Children { get; set; }
        }

        [DataContract]
        private sealed class LocalStateFile
        {
            [DataMember(Name = "profile")]
            public LocalStateProfile Profile { get; set; }
        }

        [DataContract]
        private sealed class LocalStateProfile
        {
            [DataMember(Name = "info_cache")]
            public Dictionary<string, LocalStateInfo> InfoCache { get; set; }
        }

        [DataContract]
        private sealed class LocalStateInfo
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }
        }
    }
}
