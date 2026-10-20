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
