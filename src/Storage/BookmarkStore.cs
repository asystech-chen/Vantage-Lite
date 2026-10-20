using System;
using System.Collections.Generic;

namespace VantageLite.Storage
{
    /// <summary>
    /// 书签集合：按 URL 去重，最新添加的排在最前。
    /// 依赖：JsonFile。改动注意：所有修改立即落盘。
    /// </summary>
    public sealed class BookmarkStore
    {
        private readonly string _path;
        private readonly List<Bookmark> _items;

        public BookmarkStore(string path)
        {
            _path = path;
            _items = JsonFile.Load(path, new List<Bookmark>()) ?? new List<Bookmark>();
        }

        public IList<Bookmark> Items
        {
            get { return _items; }
        }

        public bool Contains(string url)
        {
            return IndexOf(url) >= 0;
        }

        /// <summary>
        /// 有则删除、无则添加。返回 true 表示本次是添加。
        /// </summary>
        public bool Toggle(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            int index = IndexOf(url);
            if (index >= 0)
            {
                _items.RemoveAt(index);
                Save();
                return false;
            }

            _items.Insert(0, new Bookmark
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                Url = url,
                CreatedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            Save();
            return true;
        }

        public void Save()
        {
            JsonFile.Save(_path, _items);
        }

        private int IndexOf(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return -1;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
