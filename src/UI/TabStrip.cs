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
    /// 依赖：BrowserTab（只读展示）/ ThemeManager / IMessageFilter（滚轮横向滚动）。
    /// 改动注意：Sync 必须保持控件顺序与标签顺序一致；布局与滚动在 TabStrip.Scroll.cs。
    /// </summary>
    public sealed partial class TabStrip : UserControl, IMessageFilter
    {
        public event EventHandler<int> TabSelected;
        public event EventHandler<int> TabCloseRequested;
        public event EventHandler NewTabRequested;
        public event EventHandler DragRequested;
        public event EventHandler ToggleMaximizeRequested;

        private readonly Panel _viewport;
        private readonly FlowLayoutPanel _panel;
        private readonly Button _newTabButton;
        private readonly Dictionary<int, TabViewItem> _items;
        private readonly ToolTip _toolTip;
        private readonly Font _measureFont;

        // 记住最近一次 Sync 的数据，窗口宽度变化时据此重排标签。
        private IList<BrowserTab> _lastTabs;
        private BrowserTab _lastActive;

        // 中键在标签上按下后，这次点击的 MouseUp 不应再触发“空白处新建标签”。
        // 用状态位而不是超时，慢速松开也不会误开新标签。
        private bool _middleDownOnTab;

        // 左键在空白处按下后，移动超过阈值才算拖动窗口，避免吞掉双击最大化。
        private bool _leftDown;
        private Point _leftDownPoint;

        public TabStrip()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            DoubleBuffered = true;

            _items = new Dictionary<int, TabViewItem>();
            _toolTip = new ToolTip();
            ThemeManager.ApplyToolTip(_toolTip);
            _measureFont = ThemeFonts.Ui(9F);

            Dock = DockStyle.Top;
            Height = WindowChrome.TitleBarHeight;
            // 顶部不留内边距：标签直接贴住窗口上缘；底部只留 4px 收边。
            Padding = new Padding(8, 0, 8, 4);

            _viewport = new Panel();
            _viewport.Dock = DockStyle.Fill;
            _viewport.MouseUp += TabStrip_MouseUp;
            _viewport.MouseDown += TabStrip_MouseDown;
            _viewport.MouseMove += TabStrip_MouseMove;
            _viewport.DoubleClick += TabStrip_DoubleClick;
            _viewport.SizeChanged += delegate { LayoutTabs(true); };

            _panel = new BufferedFlowLayoutPanel();
            _panel.WrapContents = false;
            _panel.AutoScroll = false;
            _panel.FlowDirection = FlowDirection.LeftToRight;
            _panel.Padding = Padding.Empty;
            _panel.Margin = Padding.Empty;
            _panel.MouseUp += TabStrip_MouseUp;
            _panel.MouseDown += TabStrip_MouseDown;
            _panel.MouseMove += TabStrip_MouseMove;
            _panel.DoubleClick += TabStrip_DoubleClick;
            _viewport.Controls.Add(_panel);

            MouseUp += TabStrip_MouseUp;
            MouseDown += TabStrip_MouseDown;
            MouseMove += TabStrip_MouseMove;
            DoubleClick += TabStrip_DoubleClick;

            _newTabButton = new FlatButton();
            _newTabButton.Text = IconFont.Add;
            _newTabButton.Width = 30;
            _newTabButton.Height = 28;
            _newTabButton.FlatStyle = FlatStyle.Flat;
            _newTabButton.FlatAppearance.BorderSize = 0;
            _newTabButton.Font = IconFont.CreatePx(15f);
            _newTabButton.TabStop = false;
            _newTabButton.Cursor = Cursors.Default;
            _newTabButton.Click += delegate { Raise(NewTabRequested); };
            _toolTip.SetToolTip(_newTabButton, "新建标签页 (Ctrl+T)");
            // 按钮直接挂在视口上：未溢出时跟随最后一个标签，溢出时固定在最右侧。
            _viewport.Controls.Add(_newTabButton);
            // Controls.Add 追加到集合末尾（Z 序在后面），必须提到最前，否则会被滚动面板盖住。
            _newTabButton.BringToFront();

            Controls.Add(_viewport);

            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
            Application.AddMessageFilter(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyMetrics();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyMetrics();
            LayoutTabs(true);
        }

        private void ApplyMetrics()
        {
            int side = WindowChrome.Scale(this, 8);
            Padding = new Padding(side, 0, side, WindowChrome.Scale(this, 4));

            if (_newTabButton != null)
            {
                _newTabButton.Width = WindowChrome.Scale(this, 30);
                _newTabButton.Height = WindowChrome.Scale(this, 28);
            }

            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
                _toolTip.Dispose();
                _measureFont.Dispose();
            }

            base.Dispose(disposing);
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            Color backdrop = WindowChrome.BackdropColor;
            BackColor = backdrop;

            if (_viewport != null)
            {
                _viewport.BackColor = backdrop;
            }

            if (_panel != null)
            {
                _panel.BackColor = backdrop;
            }

            if (_newTabButton != null)
            {
                _newTabButton.UseVisualStyleBackColor = false;
                _newTabButton.BackColor = backdrop;
                _newTabButton.ForeColor = palette.TextSecondary;
                _newTabButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                _newTabButton.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            }

            foreach (TabViewItem item in _items.Values)
            {
                item.ApplyTheme();
            }

            Invalidate();
        }

        public void Sync(IList<BrowserTab> tabs, BrowserTab activeTab)
        {
            _lastTabs = tabs;
            _lastActive = activeTab;

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

            // 整批修改期间暂停布局，避免新建标签先以默认宽度出现、再被压缩导致闪烁。
            _panel.SuspendLayout();

            try
            {
                for (int i = 0; i < removed.Count; i++)
                {
                    TabViewItem item = _items[removed[i]];
                    _items.Remove(removed[i]);
                    _panel.Controls.Remove(item);
                    item.Dispose();
                }

                int buttonWidth = _newTabButton == null ? 0 : _newTabButton.Width + 2;
                int width = CalculateTabWidth(
                    tabs.Count,
                    Math.Max(1, _viewport.ClientSize.Width - buttonWidth));

                for (int i = 0; i < tabs.Count; i++)
                {
                    BrowserTab tab = tabs[i];
                    TabViewItem item;

                    if (!_items.TryGetValue(tab.Id, out item))
                    {
                        item = new TabViewItem(tab.Id);
                        item.Width = width;
                        item.SelectRequested += OnItemSelectRequested;
                        item.CloseRequested += OnItemCloseRequested;
                        item.MiddleCloseRequested += OnItemMiddleCloseRequested;
                        _items[tab.Id] = item;
                        _panel.Controls.Add(item);
                    }

                    item.Update(tab, tab == activeTab);
                    _toolTip.SetToolTip(item, tab.DisplayTitle + "\n" + (tab.Url ?? string.Empty));
                    _panel.Controls.SetChildIndex(item, Math.Min(i, _panel.Controls.Count - 1));
                }

                // 在恢复布局前就把宽度和滚动偏移算好，避免先按旧偏移绘制、
                // 再滚到新标签造成标签栏满时新建标签的闪烁。
                LayoutTabs(true);
            }
            finally
            {
                // 必须立即执行布局：ResumeLayout(false) 会留下待处理布局，
                // 新标签要等到下一次外部布局才出现，表现为“标签栏半天不刷新”。
                _panel.ResumeLayout(true);
            }
        }

        /// <summary>
        /// 中键点击标签栏空白处新建标签页。点击标签本身不会冒泡到这里。
        /// </summary>
        private void TabStrip_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _leftDown = true;
                _leftDownPoint = e.Location;
                return;
            }

            if (e.Button == MouseButtons.Middle)
            {
                // 空白处按下：这是一次全新的中键点击，解除上一条标签的抑制状态。
                _middleDownOnTab = false;
            }
        }

        private void TabStrip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_leftDown || (e.Button & MouseButtons.Left) == 0)
            {
                return;
            }

            if (Math.Abs(e.X - _leftDownPoint.X) < 5 && Math.Abs(e.Y - _leftDownPoint.Y) < 5)
            {
                return;
            }

            _leftDown = false;
            Raise(DragRequested);
        }

        private void TabStrip_DoubleClick(object sender, EventArgs e)
        {
            _leftDown = false;
            Raise(ToggleMaximizeRequested);
        }

        private void TabStrip_MouseUp(object sender, MouseEventArgs e)
        {
            _leftDown = false;

            if (e.Button != MouseButtons.Middle)
            {
                return;
            }

            if (_middleDownOnTab)
            {
                _middleDownOnTab = false;
                return;
            }

            Raise(NewTabRequested);
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

        private void OnItemMiddleCloseRequested(object sender, EventArgs e)
        {
            TabViewItem item = sender as TabViewItem;
            if (item == null)
            {
                return;
            }

            // 标记这次中键按下来自标签；松手时无论间隔多久都不新建标签。
            _middleDownOnTab = true;

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

    /// <summary>
    /// 双缓冲的 FlowLayoutPanel：标签增删/宽度重算时避免背景闪烁。
    /// </summary>
    internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            DoubleBuffered = true;
        }
    }
}
