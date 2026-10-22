using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Browser;
using VantageLite.Diagnostics;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 标签条的宽度计算、横向滚动、两端渐变与拖拽排序（partial）。
    /// 依赖：TabViewItem.SetFade / ThemeManager。改动注意：标签位置按 index*(width+2) 计算，
    /// 与 FlowLayoutPanel 的 Margin(0,0,2,0) 保持一致；不要依赖控件布局尚未刷新时的 Left。
    /// </summary>
    public sealed partial class TabStrip
    {
        private const int MaxTabWidth = 200;
        private const int MinTabWidth = 56;
        private const int ItemGap = 2;
        private const int FadeWidth = 26;
        private const int ScrollStep = 90;

        private int _scrollOffset;
        private int _maxScroll;
        private int _currentTabWidth;

        // 拖拽排序状态：左键按住标签超过阈值后进入拖拽，空白处拖动窗口不受影响。
        private TabViewItem _dragItem;
        private bool _dragArmed;
        private bool _dragging;
        private Point _dragStartScreen;
        private Point _dragLastScreen;
        private int _dropIndex = -1;
        private int _dragScrollDirection;

        /// <summary>
        /// 重算所有标签宽度、滚动范围，并让活动标签保持可见。
        /// </summary>
        private void LayoutTabs(bool ensureActiveVisible)
        {
            if (_lastTabs == null || _panel == null || _viewport == null)
            {
                return;
            }

            int viewportWidth = _viewport.ClientSize.Width;
            int viewportHeight = _viewport.ClientSize.Height;

            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                return;
            }

            if (_lastTabs.Count == 0)
            {
                _scrollOffset = 0;
                _maxScroll = 0;
                _currentTabWidth = 0;
                _panel.SetBounds(0, 0, viewportWidth, viewportHeight);
                return;
            }

            int count = _lastTabs.Count;
            int buttonWidth = _newTabButton == null ? 0 : _newTabButton.Width + ItemGap;
            int tabsViewport = Math.Max(1, viewportWidth - buttonWidth);
            int width = CalculateTabWidth(count, tabsViewport);
            _currentTabWidth = width;
            int tabsTotal = count * (width + ItemGap);

            for (int i = 0; i < count; i++)
            {
                TabViewItem item;
                if (_items.TryGetValue(_lastTabs[i].Id, out item))
                {
                    item.Width = width;
                    item.Height = viewportHeight;
                }
            }

            // 标签总宽放不下时才启用滚动；此时 + 固定在右侧，不参与滚动。
            bool overflow = tabsTotal > tabsViewport;
            _maxScroll = overflow ? tabsTotal - tabsViewport : 0;

            if (ensureActiveVisible && overflow)
            {
                int activeIndex = IndexOfActive();
                if (activeIndex >= 0)
                {
                    int left = activeIndex * (width + ItemGap);
                    int right = left + width;

                    if (left < _scrollOffset)
                    {
                        _scrollOffset = left;
                    }
                    else if (right > _scrollOffset + tabsViewport)
                    {
                        _scrollOffset = right - tabsViewport;
                    }
                }
            }

            if (!overflow)
            {
                _scrollOffset = 0;
            }

            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, _maxScroll));

            int panelWidth = Math.Max(tabsTotal, overflow ? tabsViewport : viewportWidth);
            _panel.SetBounds(-_scrollOffset, 0, panelWidth, viewportHeight);

            Log.Debug("tab layout: dpi=" + DeviceDpi + " viewport=" + viewportWidth + "x" + viewportHeight +
                " count=" + count + " tabWidth=" + width + " button=" + (_newTabButton == null ? 0 : _newTabButton.Width) +
                "x" + (_newTabButton == null ? 0 : _newTabButton.Height));

            if (_newTabButton != null)
            {
                _newTabButton.Left = overflow
                    ? viewportWidth - _newTabButton.Width
                    : tabsTotal;
                _newTabButton.Top = Math.Max(0, (viewportHeight - _newTabButton.Height) / 2);
            }

            UpdateFades(width, tabsViewport);
        }

        private int CalculateTabWidth(int tabCount, int available)
        {
            if (tabCount <= 0)
            {
                return ScaledMaxTabWidth;
            }

            int natural = available / tabCount - ItemGap;

            return Math.Max(CalculateMinTabWidth(), Math.Min(ScaledMaxTabWidth, natural));
        }

        /// <summary>
        /// 最小宽度：favicon + 两个汉字。低于这个宽度就改为横向滚动，不再继续压缩。
        /// </summary>
        private int CalculateMinTabWidth()
        {
            int text = TextRenderer.MeasureText("国国", _measureFont).Width;
            // 8(左) + 16(图标) + 6(间距) + 10(右)，全部按 DPI 缩放。
            int width = WindowChrome.Scale(this, 40) + text;
            return Math.Max(ScaledMinTabWidth, width);
        }

        private int ScaledMaxTabWidth
        {
            get { return WindowChrome.Scale(this, MaxTabWidth); }
        }

        private int ScaledMinTabWidth
        {
            get { return WindowChrome.Scale(this, MinTabWidth); }
        }

        private int ScaledFadeWidth
        {
            get { return WindowChrome.Scale(this, FadeWidth); }
        }

        private int ScaledScrollStep
        {
            get { return WindowChrome.Scale(this, ScrollStep); }
        }

        private int IndexOfActive()
        {
            if (_lastTabs == null || _lastActive == null)
            {
                return -1;
            }

            for (int i = 0; i < _lastTabs.Count; i++)
            {
                if (ReferenceEquals(_lastTabs[i], _lastActive))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ScrollBy(int delta)
        {
            if (_maxScroll <= 0 || _lastTabs == null)
            {
                return;
            }

            int next = Math.Max(0, Math.Min(_scrollOffset + delta, _maxScroll));

            if (next == _scrollOffset)
            {
                return;
            }

            _scrollOffset = next;
            LayoutTabs(false);
        }

        /// <summary>
        /// 两端只在还能继续滚动的一侧绘制渐变，提示标签被裁切。
        /// </summary>
        private void UpdateFades(int width, int tabsViewport)
        {
            int fadeWidth = ScaledFadeWidth;
            int leftFade = _scrollOffset > 0 ? fadeWidth : 0;
            int rightFade = _scrollOffset < _maxScroll ? fadeWidth : 0;

            for (int i = 0; i < _lastTabs.Count; i++)
            {
                TabViewItem item;
                if (!_items.TryGetValue(_lastTabs[i].Id, out item))
                {
                    continue;
                }

                int left = i * (width + ItemGap) - _scrollOffset;
                int leftPixels = Clamp(leftFade - left, 0, width);
                int rightPixels = Clamp((left + width) - (tabsViewport - rightFade), 0, width);

                item.SetFade(leftPixels, rightPixels);
            }
        }

        private void OnItemMouseDown(object sender, MouseEventArgs e)
        {
            TabViewItem item = sender as TabViewItem;

            if (item == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            CancelDrag();
            _dragItem = item;
            _dragArmed = true;
            _dragStartScreen = Control.MousePosition;
        }

        private void OnItemMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || _dragItem == null)
            {
                return;
            }

            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                CancelDrag();
                return;
            }

            Point screen = Control.MousePosition;

            if (!_dragging)
            {
                int threshold = WindowChrome.Scale(this, 5);

                if (Math.Abs(screen.X - _dragStartScreen.X) < threshold &&
                    Math.Abs(screen.Y - _dragStartScreen.Y) < threshold)
                {
                    return;
                }

                BeginDrag();
            }

            _dragLastScreen = screen;
            UpdateDropTarget(screen);
        }

        private void OnItemMouseUp(object sender, MouseEventArgs e)
        {
            TabViewItem item = sender as TabViewItem;

            if (item == null)
            {
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                RaiseContextMenu(item.TabId, Control.MousePosition);
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (_dragging)
            {
                EndDrag(true);
            }
            else
            {
                CancelDrag();
            }
        }

        private void BeginDrag()
        {
            if (_dragItem == null)
            {
                return;
            }

            _dragging = true;
            _dropIndex = -1;
            _dragItem.SuppressNextSelect();
            _dragStartScreen = Control.MousePosition;
            _dragLastScreen = _dragStartScreen;

            int top = WindowChrome.Scale(this, 4);
            int height = Math.Max(1, _viewport.ClientSize.Height - WindowChrome.Scale(this, 8));

            _dropIndicator.SetBounds(0, top, Math.Max(1, WindowChrome.Scale(this, 2)), height);
            _dropIndicator.Visible = true;
            _dropIndicator.BringToFront();
            _dragScrollTimer.Start();
        }

        /// <summary>
        /// 根据指针位置计算插入槽位（0..标签数），并更新指示线。
        /// </summary>
        private void UpdateDropTarget(Point screenPoint)
        {
            if (!_dragging || _lastTabs == null || _lastTabs.Count == 0)
            {
                return;
            }

            int count = _lastTabs.Count;
            int step = _currentTabWidth + ItemGap;

            if (step <= 0)
            {
                return;
            }

            Point client = _viewport.PointToClient(screenPoint);
            int raw = (int)Math.Round((client.X + _scrollOffset) / (double)step);
            int insertion = Math.Max(0, Math.Min(count, raw));

            _dropIndex = insertion;

            int x = insertion * step - _scrollOffset - 1;

            if (insertion >= count)
            {
                x = count * step - _scrollOffset - ItemGap;
            }

            _dropIndicator.Left = x;

            int edge = WindowChrome.Scale(this, 28);
            _dragScrollDirection = 0;

            if (_maxScroll > 0)
            {
                if (client.X < edge)
                {
                    _dragScrollDirection = -1;
                }
                else if (client.X > _viewport.ClientSize.Width - edge)
                {
                    _dragScrollDirection = 1;
                }
            }
        }

        private void EndDrag(bool commit)
        {
            TabViewItem item = _dragItem;
            int insertion = _dropIndex;
            int from = item == null ? -1 : IndexOfItem(item);

            CancelDrag();

            if (!commit || item == null || from < 0 || insertion < 0 || _lastTabs == null)
            {
                return;
            }

            int count = _lastTabs.Count;

            if (count == 0)
            {
                return;
            }

            // 指示线是移除前的槽位：移除后其右侧的槽位整体左移一格。
            int to = insertion > from ? insertion - 1 : insertion;
            to = Math.Max(0, Math.Min(to, count - 1));

            if (to == from)
            {
                return;
            }

            EventHandler<TabReorderEventArgs> handler = TabReorderRequested;

            if (handler != null)
            {
                handler(this, new TabReorderEventArgs(from, to));
            }
        }

        private void CancelDrag()
        {
            TabViewItem item = _dragItem;

            _dragArmed = false;
            _dragging = false;
            _dragItem = null;
            _dragScrollDirection = 0;
            _dropIndex = -1;

            if (_dragScrollTimer != null)
            {
                _dragScrollTimer.Stop();
            }

            if (_dropIndicator != null)
            {
                _dropIndicator.Visible = false;
            }

            if (item != null && item.Capture)
            {
                item.Capture = false;
            }
        }

        private void DragScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!_dragging || _dragScrollDirection == 0)
            {
                return;
            }

            ScrollBy(_dragScrollDirection * WindowChrome.Scale(this, 24));
            UpdateDropTarget(_dragLastScreen);
        }

        private int IndexOfItem(TabViewItem item)
        {
            if (_lastTabs == null || item == null)
            {
                return -1;
            }

            for (int i = 0; i < _lastTabs.Count; i++)
            {
                TabViewItem candidate;

                if (_items.TryGetValue(_lastTabs[i].Id, out candidate) && ReferenceEquals(candidate, item))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        /// <summary>
        /// 鼠标滚轮在标签条上时横向滚动，避免继续传给页面。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (_maxScroll <= 0 || !IsHandleCreated || IsDisposed || !Visible)
            {
                return false;
            }

            bool vertical = m.Msg == 0x020A;   // WM_MOUSEWHEEL
            bool horizontal = m.Msg == 0x020E; // WM_MOUSEHWHEEL

            if (!vertical && !horizontal)
            {
                return false;
            }

            if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                return false;
            }

            int delta = (short)((long)m.WParam >> 16);

            if (delta == 0)
            {
                return false;
            }

            if (vertical)
            {
                ScrollBy(delta > 0 ? -ScaledScrollStep : ScaledScrollStep);
            }
            else
            {
                ScrollBy(delta > 0 ? ScaledScrollStep : -ScaledScrollStep);
            }

            return true;
        }
    }
}
