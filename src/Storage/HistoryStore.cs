using System;
using System.Collections.Generic;

namespace VantageLite.Storage
{
    /// <summary>
    /// 浏览历史：新的在前，最多保留 2000 条，每 20 条落盘一次。
    /// 依赖：JsonFile。改动注意：退出前必须调用 Save。
    /// </summary>
    public sealed class HistoryStore
    {
        private const int MaxEntries = 2000;
        // 每次访问都落盘：应用被强制结束（关控制台窗口）时也不会丢最近记录。
        private const int SaveEvery = 1;

        private readonly string _path;
        private readonly List<HistoryEntry> _items;
        private int _unsaved;

        public HistoryStore(string path)
        {
            _path = path;
            _items = JsonFile.Load(path, new List<HistoryEntry>()) ?? new List<HistoryEntry>();
        }

        public IList<HistoryEntry> Items
        {
            get { return _items; }
        }

        public void Add(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url) || IsInternal(url))
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 同一 URL 两秒内重复访问不重复记录（重定向、刷新）。
            if (_items.Count > 0 &&
                string.Equals(_items[0].Url, url, StringComparison.OrdinalIgnoreCase) &&
                now - _items[0].VisitedUtc < 2)
            {
                return;
            }

            _items.Insert(0, new HistoryEntry
            {
                Url = url,
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                VisitedUtc = now
            });

            if (_items.Count > MaxEntries)
            {
                _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
            }

            _unsaved++;
            if (_unsaved >= SaveEvery)
            {
                Save();
            }
        }

        public void Clear()
        {
            _items.Clear();
            Save();
        }

        public void Save()
        {
            JsonFile.Save(_path, _items);
            _unsaved = 0;
        }

        private static bool IsInternal(string url)
        {
            return url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("vantage:", StringComparison.OrdinalIgnoreCase)
                || url == "about:blank";
        }
    }
}
