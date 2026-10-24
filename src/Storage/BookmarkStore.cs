using System;
using System.Collections.Generic;
using System.IO;
using VantageLite.Diagnostics;

namespace VantageLite.Storage
{
    /// <summary>
    /// 书签树：根下固定两个系统文件夹（书签工具栏 / 其他书签）。
    /// 依赖：JsonFile。改动注意：系统文件夹不能删除或重命名；旧版扁平 JSON 会自动迁移。
    /// </summary>
    public sealed class BookmarkStore
    {
        public const string RootId = "root";
        public const string ToolbarId = "toolbar";
        public const string OtherId = "other";

        private readonly string _path;

        public BookmarkStore(string path)
        {
            _path = path;
            Root = Load();
            Save();
        }

        public BookmarkNode Root { get; private set; }

        public string ClipboardId { get; set; }

        public bool ClipboardCut { get; set; }

        public string ClipboardTitle
        {
            get
            {
                BookmarkNode node = Find(ClipboardId);
                return node == null ? null : node.Title;
            }
        }

        public BookmarkNode ToolbarFolder
        {
            get { return Find(ToolbarId); }
        }

        public BookmarkNode OtherFolder
        {
            get { return Find(OtherId); }
        }

        public BookmarkNode Find(string id)
        {
            return FindIn(Root, id);
        }

        public BookmarkNode ParentOf(string id)
        {
            return FindParent(Root, id);
        }

        public bool Contains(string url)
        {
            return FindBookmark(url) != null;
        }

        /// <summary>
        /// 有则删除、无则添加到书签工具栏。返回 true 表示本次是添加。
        /// </summary>
        public bool Toggle(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            BookmarkNode existing = FindBookmark(url);
            if (existing != null)
            {
                Remove(existing.Id);
                return false;
            }

            AddBookmark(ToolbarId, title, url);
            return true;
        }

        public BookmarkNode AddFolder(string parentId, string title)
        {
            BookmarkNode parent = Find(parentId);
            if (parent == null || !parent.IsFolder)
            {
                parent = ToolbarFolder;
            }

            BookmarkNode node = new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.FolderType,
                Title = string.IsNullOrWhiteSpace(title) ? "新建文件夹" : title.Trim(),
                CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            parent.GetChildren().Add(node);
            Save();
            return node;
        }

        public BookmarkNode AddBookmark(string parentId, string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            BookmarkNode parent = Find(parentId);
            if (parent == null || !parent.IsFolder)
            {
                parent = ToolbarFolder;
            }

            BookmarkNode node = new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = BookmarkNode.BookmarkType,
                Title = string.IsNullOrWhiteSpace(title) ? url : title.Trim(),
                Url = url,
                CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            parent.GetChildren().Add(node);
            Save();
            return node;
        }

        public bool Rename(string id, string title)
        {
            if (IsSystemFolder(id) || string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            BookmarkNode node = Find(id);
            if (node == null)
            {
                return false;
            }

            node.Title = title.Trim();
            Save();
            return true;
        }

        /// <summary>
        /// 修改书签网址。只接受常见协议；与已有书签重复时拒绝。
        /// </summary>
        public bool UpdateUrl(string id, string url)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            BookmarkNode node = Find(id);

            if (node == null || node.IsFolder)
            {
                return false;
            }

            string normalized = NormalizeBookmarkUrl(url);

            if (normalized == null)
            {
                return false;
            }

            BookmarkNode existing = FindBookmark(normalized);

            if (existing != null && !string.Equals(existing.Id, id, StringComparison.Ordinal))
            {
                return false;
            }

            bool titleWasUrl = string.Equals((node.Title ?? string.Empty).Trim(), node.Url, StringComparison.OrdinalIgnoreCase);
            node.Url = normalized;

            if (titleWasUrl)
            {
                node.Title = normalized;
            }

            Save();
            return true;
        }

        private static string NormalizeBookmarkUrl(string value)
        {
            value = value.Trim();

            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                // 允许直接输入 example.com，补 https。
                if (value.IndexOf(' ') < 0 && value.IndexOf('.') >= 0)
                {
                    value = "https://" + value;
                }

                if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                {
                    return null;
                }
            }

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "http":
                case "https":
                case "ftp":
                case "file":
                case "mailto":
                    return value;
                default:
                    return null;
            }
        }

        public bool Cut(string id)
        {
            if (string.IsNullOrEmpty(id) || IsSystemFolder(id) || Find(id) == null)
            {
                return false;
            }

            ClipboardId = id;
            ClipboardCut = true;
            return true;
        }

        public bool CopyToClipboard(string id)
        {
            if (string.IsNullOrEmpty(id) || IsSystemFolder(id) || Find(id) == null)
            {
                return false;
            }

            ClipboardId = id;
            ClipboardCut = false;
            return true;
        }

        public bool Paste(string parentId)
        {
            BookmarkNode target = Find(parentId);
            if (target == null || !target.IsFolder || string.IsNullOrEmpty(ClipboardId))
            {
                return false;
            }

            if (ClipboardCut)
            {
                bool moved = Move(ClipboardId, parentId);
                if (moved)
                {
                    ClipboardId = null;
                    ClipboardCut = false;
                }

                return moved;
            }

            return Copy(ClipboardId, parentId) != null;
        }

        public bool Move(string id, string newParentId)
        {
            return Move(id, newParentId, null);
        }

        public bool Move(string id, string newParentId, string beforeId)
        {
            if (IsSystemFolder(id))
            {
                return false;
            }

            BookmarkNode node = Find(id);
            BookmarkNode target = Find(newParentId);

            if (node == null || target == null || !target.IsFolder)
            {
                return false;
            }

            // 文件夹不能移动到自身或自己的子级。
            if (node.IsFolder && IsDescendantOrSelf(id, newParentId))
            {
                return false;
            }

            BookmarkNode parent = ParentOf(id);
            if (parent == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(beforeId) &&
                string.Equals(beforeId, id, StringComparison.Ordinal))
            {
                return false;
            }

            parent.GetChildren().Remove(node);

            if (!string.IsNullOrEmpty(beforeId))
            {
                IList<BookmarkNode> children = target.GetChildren();
                int index = -1;

                for (int i = 0; i < children.Count; i++)
                {
                    if (string.Equals(children[i].Id, beforeId, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    children.Insert(index, node);
                }
                else
                {
                    children.Add(node);
                }
            }
            else
            {
                target.GetChildren().Add(node);
            }

            Save();
            return true;
        }

        public BookmarkNode Copy(string id, string newParentId)
        {
            BookmarkNode node = Find(id);
            BookmarkNode target = Find(newParentId);

            if (node == null || target == null || !target.IsFolder)
            {
                return null;
            }

            if (node.IsFolder && IsDescendantOrSelf(id, newParentId))
            {
                return null;
            }

            BookmarkNode clone = CloneNode(node);
            target.GetChildren().Add(clone);
            Save();
            return clone;
        }

        public bool IsDescendantOrSelf(string ancestorId, string nodeId)
        {
            BookmarkNode ancestor = Find(ancestorId);
            return ancestor != null && ContainsNode(ancestor, nodeId);
        }

        private static BookmarkNode CloneNode(BookmarkNode node)
        {
            BookmarkNode clone = new BookmarkNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = node.Type,
                Title = node.Title,
                Url = node.Url,
                CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (node.IsFolder && node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    clone.GetChildren().Add(CloneNode(node.Children[i]));
                }
            }

            return clone;
        }

        private static bool ContainsNode(BookmarkNode root, string id)
        {
            if (root == null)
            {
                return false;
            }

            if (string.Equals(root.Id, id, StringComparison.Ordinal))
            {
                return true;
            }

            if (root.Children == null)
            {
                return false;
            }

            for (int i = 0; i < root.Children.Count; i++)
            {
                if (ContainsNode(root.Children[i], id))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || IsSystemFolder(id))
            {
                return false;
            }

            BookmarkNode parent = FindParent(Root, id);
            if (parent == null)
            {
                return false;
            }

            IList<BookmarkNode> children = parent.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (string.Equals(children[i].Id, id, StringComparison.Ordinal))
                {
                    children.RemoveAt(i);
                    Save();
                    return true;
                }
            }

            return false;
        }

        public void Save()
        {
            JsonFile.Save(_path, Root);
        }

        private BookmarkNode Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return CreateDefault();
                }

                string text = File.ReadAllText(_path).TrimStart();

                // 旧版是扁平数组，迁移到「其他书签」。
                if (text.StartsWith("[", StringComparison.Ordinal))
                {
                    List<Bookmark> legacy = JsonFile.Load(_path, new List<Bookmark>()) ?? new List<Bookmark>();
                    BookmarkNode root = CreateDefault();
                    BookmarkNode other = FindIn(root, OtherId);

                    for (int i = legacy.Count - 1; i >= 0; i--)
                    {
                        Bookmark item = legacy[i];
                        if (item == null || string.IsNullOrWhiteSpace(item.Url))
                        {
                            continue;
                        }

                        other.GetChildren().Add(new BookmarkNode
                        {
                            Id = string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                            Type = BookmarkNode.BookmarkType,
                            Title = string.IsNullOrWhiteSpace(item.Title) ? item.Url : item.Title,
                            Url = item.Url,
                            CreatedUtc = item.CreatedUtc
                        });
                    }

                    Log.Info("Migrated " + legacy.Count + " legacy bookmarks");
                    return root;
                }

                return EnsureSystem(JsonFile.Load<BookmarkNode>(_path, null));
            }
            catch (Exception ex)
            {
                Log.Warn("读取书签失败：" + ex.Message);
                return CreateDefault();
            }
        }

        private static BookmarkNode CreateDefault()
        {
            BookmarkNode root = new BookmarkNode
            {
                Id = RootId,
                Type = BookmarkNode.FolderType,
                Title = "书签"
            };

            root.GetChildren().Add(new BookmarkNode
            {
                Id = ToolbarId,
                Type = BookmarkNode.FolderType,
                Title = "书签工具栏"
            });

            root.GetChildren().Add(new BookmarkNode
            {
                Id = OtherId,
                Type = BookmarkNode.FolderType,
                Title = "其他书签"
            });

            return root;
        }

        private static BookmarkNode EnsureSystem(BookmarkNode root)
        {
            if (root == null)
            {
                return CreateDefault();
            }

            if (root.Children == null)
            {
                root.Children = new List<BookmarkNode>();
            }

            if (FindIn(root, ToolbarId) == null)
            {
                root.Children.Insert(0, new BookmarkNode
                {
                    Id = ToolbarId,
                    Type = BookmarkNode.FolderType,
                    Title = "书签工具栏"
                });
            }

            if (FindIn(root, OtherId) == null)
            {
                root.Children.Add(new BookmarkNode
                {
                    Id = OtherId,
                    Type = BookmarkNode.FolderType,
                    Title = "其他书签"
                });
            }

            return root;
        }

        private static BookmarkNode FindIn(BookmarkNode node, string id)
        {
            if (node == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (string.Equals(node.Id, id, StringComparison.Ordinal))
            {
                return node;
            }

            if (node.Children == null)
            {
                return null;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode found = FindIn(node.Children[i], id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static BookmarkNode FindParent(BookmarkNode node, string id)
        {
            if (node == null || node.Children == null)
            {
                return null;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (string.Equals(node.Children[i].Id, id, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode found = FindParent(node.Children[i], id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private BookmarkNode FindBookmark(string url)
        {
            return FindBookmark(Root, url);
        }

        private static BookmarkNode FindBookmark(BookmarkNode node, string url)
        {
            if (node == null || node.Children == null)
            {
                return null;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (!child.IsFolder &&
                    string.Equals(child.Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                BookmarkNode found = FindBookmark(child, url);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsSystemFolder(string id)
        {
            return string.Equals(id, ToolbarId, StringComparison.Ordinal)
                || string.Equals(id, OtherId, StringComparison.Ordinal)
                || string.Equals(id, RootId, StringComparison.Ordinal);
        }
    }
}
