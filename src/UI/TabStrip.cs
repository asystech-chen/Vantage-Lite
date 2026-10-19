using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 标签条：显示所有标签、当前活动标签、关闭按钮和新建按钮。
    /// 依赖：BrowserTab（只读展示）/ ThemeManager。改动注意：Sync 必须保持控件顺序与标签顺序一致。
    /// </summary>
    public sealed class TabStrip : UserControl
    {
        public event EventHandler<int> TabSelected;
        public event EventHandler<int> TabCloseRequested;
        public event EventHandler NewTabRequested;

        private readonly FlowLayoutPanel _panel;
        private readonly Button _newTabButton;
        private readonly Dictionary<int, TabViewItem> _items;
        private readonly ToolTip _toolTip;

        public TabStrip()
        {
            _items = new Dictionary<int, TabViewItem>();
            _toolTip = new ToolTip();

            Dock = DockStyle.Top;
            Height = 36;
            Padding = new Padding(8, 4, 8, 0);

            _panel = new FlowLayoutPanel();
            _panel.Dock = DockStyle.Fill;
            _panel.WrapContents = false;
            _panel.AutoScroll = true;
            _panel.FlowDirection = FlowDirection.LeftToRight;
            _panel.Padding = Padding.Empty;
            _panel.Margin = Padding.Empty;

            _newTabButton = new Button();
            _newTabButton.Text = IconFont.Add;
            _newTabButton.Width = 30;
            _newTabButton.Height = 28;
            _newTabButton.FlatStyle = FlatStyle.Flat;
            _newTabButton.FlatAppearance.BorderSize = 0;
            _newTabButton.Font = IconFont.Create(11F);
            _newTabButton.Margin = new Padding(2, 0, 0, 0);
            _newTabButton.TabStop = false;
            _newTabButton.Cursor = Cursors.Default;
            _newTabButton.Click += delegate { Raise(NewTabRequested); };
            _toolTip.SetToolTip(_newTabButton, "新建标签页 (Ctrl+T)");

            _panel.Controls.Add(_newTabButton);
            Controls.Add(_panel);

            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.TabStripBackground;
            _panel.BackColor = palette.TabStripBackground;
            _newTabButton.BackColor = palette.TabStripBackground;
            _newTabButton.ForeColor = palette.TextSecondary;
            _newTabButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            _newTabButton.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;

            foreach (TabViewItem item in _items.Values)
            {
                item.ApplyTheme();
            }
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

            int width = CalculateTabWidth(tabs.Count);

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

                item.Width = width;
                item.Update(tab, tab == activeTab);
                _toolTip.SetToolTip(item, tab.DisplayTitle + "\n" + (tab.Url ?? string.Empty));
                _panel.Controls.SetChildIndex(item, Math.Min(i, _panel.Controls.Count - 1));
            }

            _panel.Controls.SetChildIndex(_newTabButton, _panel.Controls.Count - 1);
        }

        private int CalculateTabWidth(int tabCount)
        {
            if (tabCount <= 0)
            {
                return 180;
            }

            int available = _panel.ClientSize.Width - _newTabButton.Width - 16;
            if (available <= 0)
            {
                return 180;
            }

            int width = available / tabCount;
            return Math.Max(96, Math.Min(200, width));
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

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTheme));
                return;
            }

            ApplyTheme();
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
