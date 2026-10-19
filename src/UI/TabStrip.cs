using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Browser;

namespace VantageLite.UI
{
    /// <summary>
    /// 标签条：显示所有标签、当前活动标签、关闭按钮和新建按钮。
    /// 依赖：BrowserTab（只读展示）。改动注意：Sync 必须保持控件顺序与标签顺序一致。
    /// </summary>
    public sealed class TabStrip : UserControl
    {
        public event EventHandler<int> TabSelected;
        public event EventHandler<int> TabCloseRequested;
        public event EventHandler NewTabRequested;

        private static readonly Color StripColor = Color.FromArgb(26, 29, 36);
        private static readonly Color HoverColor = Color.FromArgb(55, 60, 72);

        private readonly FlowLayoutPanel _panel;
        private readonly Button _newTabButton;
        private readonly Dictionary<int, TabViewItem> _items;
        private readonly ToolTip _toolTip;

        public TabStrip()
        {
            _items = new Dictionary<int, TabViewItem>();
            _toolTip = new ToolTip();

            Dock = DockStyle.Top;
            Height = 34;
            BackColor = StripColor;
            Padding = new Padding(4, 2, 4, 0);

            _panel = new FlowLayoutPanel();
            _panel.Dock = DockStyle.Fill;
            _panel.WrapContents = false;
            _panel.AutoScroll = true;
            _panel.FlowDirection = FlowDirection.LeftToRight;
            _panel.BackColor = StripColor;
            _panel.Padding = Padding.Empty;
            _panel.Margin = Padding.Empty;

            _newTabButton = new Button();
            _newTabButton.Text = "+";
            _newTabButton.Width = 28;
            _newTabButton.Height = 26;
            _newTabButton.FlatStyle = FlatStyle.Flat;
            _newTabButton.FlatAppearance.BorderSize = 0;
            _newTabButton.BackColor = StripColor;
            _newTabButton.ForeColor = Color.Gainsboro;
            _newTabButton.Font = new Font("Segoe UI", 12F);
            _newTabButton.Margin = new Padding(2, 0, 0, 0);
            _newTabButton.TabStop = false;
            _newTabButton.Cursor = Cursors.Hand;
            _newTabButton.Click += delegate { Raise(NewTabRequested); };
            _newTabButton.MouseEnter += delegate { _newTabButton.BackColor = HoverColor; };
            _newTabButton.MouseLeave += delegate { _newTabButton.BackColor = StripColor; };
            _toolTip.SetToolTip(_newTabButton, "新建标签页 (Ctrl+T)");

            _panel.Controls.Add(_newTabButton);
            Controls.Add(_panel);
        }

        public void Sync(IList<BrowserTab> tabs, BrowserTab activeTab)
        {
            HashSet<int> alive = new HashSet<int>();
            for (int i = 0; i < tabs.Count; i++)
            {
                alive.Add(tabs[i].Id);
            }

            List<int> removed = new List<int>();
            foreach (KeyValuePair<int, TabViewItem> pair in _items)
            {
                if (!alive.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int i = 0; i < removed.Count; i++)
            {
                TabViewItem item = _items[removed[i]];
                _items.Remove(removed[i]);
                _panel.Controls.Remove(item);
                item.Dispose();
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                BrowserTab tab = tabs[i];
                TabViewItem item;

                if (!_items.TryGetValue(tab.Id, out item))
                {
                    item = new TabViewItem(tab.Id);
                    item.SelectRequested += OnItemSelectRequested;
                    item.CloseRequested += OnItemCloseRequested;
                    _items[tab.Id] = item;
                    _panel.Controls.Add(item);
                }

                item.Update(tab, tab == activeTab);
                _toolTip.SetToolTip(item, tab.DisplayTitle + "\n" + (tab.Url ?? string.Empty));
                _panel.Controls.SetChildIndex(item, Math.Min(i, _panel.Controls.Count - 1));
            }

            _panel.Controls.SetChildIndex(_newTabButton, _panel.Controls.Count - 1);
        }

        private void OnItemSelectRequested(object sender, EventArgs e)
        {
            TabViewItem item = sender as TabViewItem;
            if (item == null)
            {
                return;
            }

            EventHandler<int> handler = TabSelected;
            if (handler != null)
            {
                handler(this, item.TabId);
            }
        }

        private void OnItemCloseRequested(object sender, EventArgs e)
        {
            TabViewItem item = sender as TabViewItem;
            if (item == null)
            {
                return;
            }

            EventHandler<int> handler = TabCloseRequested;
            if (handler != null)
            {
                handler(this, item.TabId);
            }
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

    }
}
