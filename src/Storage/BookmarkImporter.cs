using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VantageLite.Storage
{
    /// <summary>
    /// 书签导入：Netscape HTML 与 .url 快捷方式。
    /// 依赖：BookmarkStore。改动注意：HTML 只识别 DL/DT/H3/A 结构，其他标签跳过；
    /// 全部文件解析完成后挂载子树并只落盘一次。
    /// </summary>
    public static class BookmarkImporter
    {
        private static readonly Regex AttributePattern = new Regex(
            "([a-zA-Z_:][-a-zA-Z0-9_:.]*)\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",
            RegexOptions.Compiled);

        public sealed class ImportResult
        {
            public int Imported { get; set; }

            public int Duplicates { get; set; }

            public int Invalid { get; set; }

            public int Files { get; set; }

            public string FolderId { get; set; }

            public List<string> Errors { get; private set; } = new List<string>();

            public string Message
            {
                get
                {
                    if (Imported == 0)
                    {
                        if (Duplicates == 0 && Invalid == 0)
                        {
                            return "没有找到可导入的书签";
                        }

                        return "没有新增书签（重复 " + Duplicates + " 条，无效 " + Invalid + " 条）";
                    }

                    StringBuilder text = new StringBuilder();
                    text.Append("已导入 ").Append(Imported).Append(" 条书签");

                    if (Duplicates > 0)
                    {
                        text.Append("，跳过重复 ").Append(Duplicates).Append(" 条");
                    }

                    if (Invalid > 0)
                    {
                        text.Append("，跳过无效 ").Append(Invalid).Append(" 条");
                    }

                    return text.ToString();
                }
            }
        }

        /// <summary>
        /// 导入多个 HTML / .url 文件。书签栏内容并入「书签工具栏」，
        /// 其他内容并入「其他书签」，不再创建额外的日期文件夹。
        /// </summary>
        public static ImportResult Import(BookmarkStore store, IEnumerable<string> paths, string parentId)
        {
            ImportResult result = new ImportResult();
            if (store == null || paths == null)
            {
                return result;
            }

            HashSet<string> known = CollectExisting(store);
            BookmarkNode parsed = NewFolder("import");

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    result.Invalid++;
                    continue;
                }

                result.Files++;

                try
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();

                    if (extension == ".url")
                    {
                        ImportUrlFile(path, parsed);
                    }
                    else
                    {
                        ParseHtml(ReadText(path), parsed);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(Path.GetFileName(path) + "：" + ex.Message);
                }
            }

            MergeSourceTree(store, parsed, known, result, parentId);

            if (result.Imported > 0)
            {
                store.Save();
            }

            result.FolderId = parentId;
            return result;
        }

        /// <summary>
        /// 导入已安装浏览器的书签树：工具栏根并入「书签工具栏」，
        /// 其他根并入「其他书签」，移动设备等保留为「其他书签」下的文件夹。
        /// </summary>
        public static ImportResult ImportSources(
            BookmarkStore store,
            IEnumerable<BookmarkSources.Source> sources,
            string parentId)
        {
            ImportResult result = new ImportResult();

            if (store == null || sources == null)
            {
                return result;
            }

            HashSet<string> known = CollectExisting(store);

            foreach (BookmarkSources.Source source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                BookmarkNode tree;

                try
                {
                    tree = BookmarkSources.Load(source);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(source + "：" + ex.Message);
                    continue;
                }

                if (tree == null)
                {
                    continue;
                }

                MergeSourceTree(store, tree, known, result, parentId);
                result.Files++;
            }

            if (result.Imported > 0)
            {
                store.Save();
            }

            result.FolderId = parentId;
            return result;
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

        private static HashSet<string> CollectExisting(BookmarkStore store)
        {
            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectUrls(store.Root, known);
            return known;
        }

        /// <summary>
        /// 按根节点的来源目标合并：toolbar/other 直接展开到系统文件夹，
        /// other-folder 作为普通文件夹合并。
        /// </summary>
        private static void MergeSourceTree(
            BookmarkStore store,
            BookmarkNode sourceRoot,
            HashSet<string> known,
            ImportResult result,
            string fallbackParentId)
        {
            BookmarkNode fallback = store.Find(fallbackParentId);

            if (fallback == null || !fallback.IsFolder)
            {
                fallback = store.OtherFolder;
            }

            BookmarkNode toolbar = store.ToolbarFolder;
            BookmarkNode other = store.OtherFolder;

            IList<BookmarkNode> roots = sourceRoot.GetChildren();

            for (int i = 0; i < roots.Count; i++)
            {
                BookmarkNode root = roots[i];
                string target = root.ImportTarget;

                if (string.Equals(target, "toolbar", StringComparison.Ordinal) ||
                    (target == null && IsToolbarFolder(root)))
                {
                    if (toolbar != null)
                    {
                        MergeChildren(toolbar, root, known, result);
                    }

                    continue;
                }

                if (string.Equals(target, "other", StringComparison.Ordinal) ||
                    (target == null && IsOtherFolder(root)))
                {
                    if (other != null)
                    {
                        MergeChildren(other, root, known, result);
                    }

                    continue;
                }

                if (string.Equals(target, "other-folder", StringComparison.Ordinal))
                {
                    if (other != null)
                    {
                        MergeNode(other, root, known, result);
                    }

                    continue;
                }

                MergeNode(fallback, root, known, result);
            }
        }

        private static void MergeChildren(
            BookmarkNode parent,
            BookmarkNode sourceFolder,
            HashSet<string> known,
            ImportResult result)
        {
            IList<BookmarkNode> children = sourceFolder.GetChildren();

            for (int i = 0; i < children.Count; i++)
            {
                MergeNode(parent, children[i], known, result);
            }
        }

        private static void MergeNode(
            BookmarkNode parent,
            BookmarkNode source,
            HashSet<string> known,
            ImportResult result)
        {
            if (parent == null || source == null)
            {
                return;
            }

            if (source.IsFolder)
            {
                string title = string.IsNullOrWhiteSpace(source.Title) ? "未命名文件夹" : source.Title.Trim();
                BookmarkNode existing = FindFolder(parent, title);

                if (existing != null)
                {
                    MergeChildren(existing, source, known, result);
                    return;
                }

                BookmarkNode folder = NewFolder(title);
                folder.CreatedUtc = source.CreatedUtc;
                parent.GetChildren().Add(folder);
                MergeChildren(folder, source, known, result);

                if (folder.GetChildren().Count == 0)
                {
                    parent.GetChildren().Remove(folder);
                }

                return;
            }

            string url = source.Url == null ? null : source.Url.Trim();

            if (!IsSupportedUrl(url))
            {
                result.Invalid++;
                return;
            }

            if (!known.Add(url))
            {
                result.Duplicates++;
                return;
            }

            parent.GetChildren().Add(new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.BookmarkType,
                Title = string.IsNullOrWhiteSpace(source.Title) ? url : source.Title.Trim(),
                Url = url,
                CreatedUtc = source.CreatedUtc > 0
                    ? source.CreatedUtc
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            result.Imported++;
        }

        private static BookmarkNode FindFolder(BookmarkNode parent, string title)
        {
            IList<BookmarkNode> children = parent.GetChildren();

            for (int i = 0; i < children.Count; i++)
            {
                BookmarkNode child = children[i];

                if (child.IsFolder &&
                    string.Equals((child.Title ?? string.Empty).Trim(), title, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsToolbarFolder(BookmarkNode node)
        {
            string title = (node == null ? null : node.Title);
            title = title == null ? string.Empty : title.Trim();

            return string.Equals(title, "Bookmarks bar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Bookmarks Toolbar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Personal Toolbar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Favorites bar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Favorites Bar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "书签栏", StringComparison.Ordinal) ||
                   string.Equals(title, "书签工具栏", StringComparison.Ordinal) ||
                   string.Equals(title, "收藏夹栏", StringComparison.Ordinal);
        }

        private static bool IsOtherFolder(BookmarkNode node)
        {
            string title = (node == null ? null : node.Title);
            title = title == null ? string.Empty : title.Trim();

            return string.Equals(title, "Other bookmarks", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Other Bookmarks", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "Other favorites", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "其他书签", StringComparison.Ordinal) ||
                   string.Equals(title, "其他收藏夹", StringComparison.Ordinal);
        }

        private static void ParseHtml(string html, BookmarkNode container)
        {
            if (string.IsNullOrEmpty(html))
            {
                return;
            }

            IList<BookmarkNode> current = container.GetChildren();
            Stack<IList<BookmarkNode>> stack = new Stack<IList<BookmarkNode>>();
            BookmarkNode pendingFolder = null;
            int index = 0;

            while (index < html.Length)
            {
                int lt = html.IndexOf('<', index);
                if (lt < 0)
                {
                    break;
                }

                if (string.CompareOrdinal(html, lt, "<!--", 0, 4) == 0)
                {
                    int commentEnd = html.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                    index = commentEnd < 0 ? html.Length : commentEnd + 3;
                    continue;
                }

                int gt = html.IndexOf('>', lt + 1);
                if (gt < 0)
                {
                    break;
                }

                string inner = html.Substring(lt + 1, gt - lt - 1).Trim();
                index = gt + 1;

                if (inner.Length == 0)
                {
                    continue;
                }

                bool closing = inner[0] == '/';
                if (closing)
                {
                    inner = inner.Substring(1).Trim();
                }

                int nameEnd = 0;
                while (nameEnd < inner.Length &&
                       !char.IsWhiteSpace(inner[nameEnd]) &&
                       inner[nameEnd] != '/')
                {
                    nameEnd++;
                }

                string name = inner.Substring(0, nameEnd).ToLowerInvariant();
                string attributes = inner.Substring(nameEnd);

                if (name == "dl")
                {
                    if (closing)
                    {
                        if (stack.Count > 0)
                        {
                            current = stack.Pop();
                        }

                        pendingFolder = null;
                    }
                    else
                    {
                        stack.Push(current);

                        if (pendingFolder != null)
                        {
                            current = pendingFolder.GetChildren();
                        }

                        pendingFolder = null;
                    }

                    continue;
                }

                if (closing)
                {
                    continue;
                }

                if (name == "h3")
                {
                    string title = ReadElementText(html, ref index, "h3");
                    BookmarkNode folder = new BookmarkNode
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = BookmarkNode.FolderType,
                        Title = CleanText(title),
                        CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    if (string.IsNullOrEmpty(folder.Title))
                    {
                        folder.Title = "新建文件夹";
                    }

                    string toolbarFlag = GetAttribute(attributes, "personal_toolbar_folder");
                    if (string.Equals(toolbarFlag, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        folder.ImportTarget = "toolbar";
                    }

                    current.Add(folder);
                    pendingFolder = folder;
                    continue;
                }

                if (name == "a")
                {
                    string href = GetAttribute(attributes, "href");
                    long created = ParseDate(GetAttribute(attributes, "add_date"));
                    string title = ReadElementText(html, ref index, "a");

                    AddParsedBookmark(current, CleanText(title), href, created);
                    pendingFolder = null;
                }
            }
        }

        private static void ImportUrlFile(string path, BookmarkNode container)
        {
            string url = null;
            string text = ReadText(path);

            using (StringReader reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.Length == 0 || line[0] == '[' || line[0] == ';')
                    {
                        continue;
                    }

                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }

                    if (string.Equals(line.Substring(0, equals).Trim(), "URL", StringComparison.OrdinalIgnoreCase))
                    {
                        url = line.Substring(equals + 1).Trim();
                        break;
                    }
                }
            }

            string title = Path.GetFileNameWithoutExtension(path);
            AddParsedBookmark(container.GetChildren(), title, url, 0);
        }

        private static void AddParsedBookmark(IList<BookmarkNode> target, string title, string url, long created)
        {
            target.Add(new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.BookmarkType,
                Title = string.IsNullOrEmpty(title) ? (url ?? string.Empty) : title,
                Url = url,
                CreatedUtc = created > 0 ? created : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        private static bool IsSupportedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "http":
                case "https":
                case "ftp":
                case "file":
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadElementText(string html, ref int index, string tagName)
        {
            string closingTag = "</" + tagName + ">";
            int end = html.IndexOf(closingTag, index, StringComparison.OrdinalIgnoreCase);

            if (end >= 0)
            {
                string text = html.Substring(index, end - index);
                index = end + closingTag.Length;
                return text;
            }

            int next = html.IndexOf('<', index);
            if (next < 0)
            {
                string rest = html.Substring(index);
                index = html.Length;
                return rest;
            }

            string partial = html.Substring(index, next - index);
            index = next;
            return partial;
        }

        private static string CleanText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder text = new StringBuilder(value.Length);
            bool inTag = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    text.Append(c);
                }
            }

            return WebUtility.HtmlDecode(text.ToString()).Trim();
        }

        private static string GetAttribute(string attributes, string name)
        {
            if (string.IsNullOrEmpty(attributes))
            {
                return null;
            }

            MatchCollection matches = AttributePattern.Matches(attributes);

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];

                if (!string.Equals(match.Groups[1].Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match.Groups[3].Success)
                {
                    return WebUtility.HtmlDecode(match.Groups[3].Value);
                }

                if (match.Groups[4].Success)
                {
                    return WebUtility.HtmlDecode(match.Groups[4].Value);
                }

                return WebUtility.HtmlDecode(match.Groups[5].Value);
            }

            return null;
        }

        private static long ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            long seconds;
            if (!long.TryParse(value.Trim(), out seconds))
            {
                return 0;
            }

            // 过滤明显异常的时间戳（1970 之前、2100 之后）。
            if (seconds <= 0 || seconds > 4102444800)
            {
                return 0;
            }

            return seconds;
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    return Encoding.GetEncoding(54936).GetString(bytes);
                }
                catch (ArgumentException)
                {
                    return Encoding.Default.GetString(bytes);
                }
            }
        }

        private static void CollectUrls(BookmarkNode node, HashSet<string> urls)
        {
            if (node == null)
            {
                return;
            }

            if (!node.IsFolder && !string.IsNullOrWhiteSpace(node.Url))
            {
                urls.Add(node.Url.Trim());
            }

            if (node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                CollectUrls(node.Children[i], urls);
            }
        }
    }
}
