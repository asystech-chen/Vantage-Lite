using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VantageLite.Downloads;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 下载面板：列表、进度、暂停/继续/取消、打开文件/目录、历史。
    /// 依赖：DownloadManager / ThemeManager。改动注意：进度走 item.Updated，不要每次重建行。
    /// </summary>
    public sealed class DownloadsForm : Form
    {
        private const int HeaderHeight = 42;
        private const int FooterHeight = 44;
        private const int RowHeight = 94;
        private const int MaxPopupHeight = 470;
        private const int MinPopupHeight = 170;

        private readonly DownloadManager _manager;
        private readonly FlowLayoutPanel _list;
        private readonly Label _empty;
        private readonly Label _title;
        private readonly FlatButton _clearButton;
        private readonly FlatButton _closeButton;
        private readonly FlatButton _viewAll;
        private readonly Dictionary<string, DownloadRow> _rows;

        /// <summary>
        /// 底部「查看所有下载」：由宿主打开 about:downloads 内置页。
        /// </summary>
        public event EventHandler ViewAllRequested;

        /// <summary>
        /// 行内「重试」：由宿主用新标签重新发起下载。
        /// </summary>
        public event EventHandler<DownloadItem> RetryRequested;

        public DownloadsForm(DownloadManager manager)
        {
            _manager = manager;
            _rows = new Dictionary<string, DownloadRow>(StringComparer.Ordinal);

            Text = "下载内容";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(340, 380);
            MinimizeBox = false;
            MaximizeBox = false;
            KeyPreview = true;
            Font = ThemeFonts.Ui(9.5F);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            // 弹出式：点到其它地方就收起。
            Deactivate += delegate
            {
                if (!IsDisposed)
                {
                    Close();
                }
            };

            _title = new Label();
            _title.Text = "本次下载";
            _title.AutoSize = true;
            _title.Font = ThemeFonts.Ui(11F);
            _title.SetBounds(14, 12, 200, 22);

            _clearButton = new FlatButton();
            _clearButton.Text = "清空已完成";
            _clearButton.Width = 88;
            _clearButton.Height = 22;
            _clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _clearButton.FlatStyle = FlatStyle.Flat;
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.Font = ThemeFonts.Ui(9F);
            _clearButton.TabStop = false;
            _clearButton.Cursor = Cursors.Default;
            _clearButton.Click += delegate { _manager.ClearFinished(); };
            _clearButton.SetBounds(ClientSize.Width - 128, 11, 88, 22);

            _closeButton = new FlatButton();
            _closeButton.Text = IconFont.Close;
            _closeButton.Width = 24;
            _closeButton.Height = 24;
            _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _closeButton.FlatStyle = FlatStyle.Flat;
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Font = IconFont.Create(8F);
            _closeButton.TabStop = false;
            _closeButton.Cursor = Cursors.Default;
            _closeButton.Click += delegate { Close(); };
            _closeButton.SetBounds(ClientSize.Width - 32, 10, 24, 24);

            _list = new FlowLayoutPanel();
            _list.SetBounds(10, 42, ClientSize.Width - 20, ClientSize.Height - 86);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.Padding = Padding.Empty;
            _list.Margin = Padding.Empty;

            _viewAll = new FlatButton();
            _viewAll.Text = "查看所有下载";
            _viewAll.Height = 26;
            _viewAll.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _viewAll.FlatStyle = FlatStyle.Flat;
            _viewAll.FlatAppearance.BorderSize = 0;
            _viewAll.Font = ThemeFonts.Ui(9F);
            _viewAll.TabStop = false;
            _viewAll.Cursor = Cursors.Default;
            _viewAll.Click += delegate
            {
                EventHandler handler = ViewAllRequested;

                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            _viewAll.SetBounds(10, ClientSize.Height - 34, ClientSize.Width - 20, 26);

            _empty = new Label();
            _empty.Text = "本次会话暂无下载";
            _empty.AutoSize = false;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.SetBounds(10, 120, ClientSize.Width - 20, 40);
            _empty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Controls.Add(_title);
            Controls.Add(_clearButton);
            Controls.Add(_closeButton);
            Controls.Add(_list);
            Controls.Add(_viewAll);
            Controls.Add(_empty);

            _list.SizeChanged += delegate { UpdateRowWidths(); };

            _manager.Changed += Manager_Changed;
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;

            LayoutChrome();
            ApplyTheme();
            RefreshItems();
        }

        public void RefreshItems()
        {
            IList<DownloadItem> items = _manager.SessionItems;
            HashSet<string> alive = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                alive.Add(items[i].Id);
            }

            List<string> removed = new List<string>();

            foreach (KeyValuePair<string, DownloadRow> pair in _rows)
            {
                if (!alive.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int i = 0; i < removed.Count; i++)
            {
                DownloadRow row = _rows[removed[i]];
                _rows.Remove(removed[i]);
                _list.Controls.Remove(row);
                row.Dispose();
            }

            for (int i = 0; i < items.Count; i++)
            {
                DownloadItem item = items[i];
                DownloadRow row;

                if (!_rows.TryGetValue(item.Id, out row))
                {
                    row = new DownloadRow(_manager, item);
                    row.RetryRequested += delegate
                    {
                        EventHandler<DownloadItem> handler = RetryRequested;

                        if (handler != null)
                        {
                            handler(this, item);
                        }
                    };
                    _rows[item.Id] = row;
                    row.Width = RowWidth();
                    _list.Controls.Add(row);
                }

                row.Width = RowWidth();
                row.UpdateItem(item);
                _list.Controls.SetChildIndex(row, Math.Min(i, _list.Controls.Count - 1));
            }

            _empty.Visible = items.Count == 0;

            AdjustHeight();
            LayoutChrome();
        }

        /// <summary>
        /// 高度随任务数增长，到上限后固定，列表内部滚动。
        /// </summary>
        private void AdjustHeight()
        {
            int count = _manager.SessionItems.Count;
            int desired = count == 0
                ? MinPopupHeight
                : HeaderHeight + count * RowHeight + FooterHeight;
            int height = Math.Max(MinPopupHeight, Math.Min(MaxPopupHeight, desired));

            if (ClientSize.Height != height)
            {
                ClientSize = new Size(ClientSize.Width, height);
            }
        }

        private void LayoutChrome()
        {
            int width = Math.Max(120, ClientSize.Width - 20);
            int listHeight = Math.Max(40, ClientSize.Height - HeaderHeight - FooterHeight);

            _list.SetBounds(10, HeaderHeight, width, listHeight);
            _viewAll.SetBounds(10, ClientSize.Height - 34, width, 26);
            _empty.SetBounds(10, HeaderHeight + Math.Max(0, (listHeight - 40) / 2), width, 40);

            UpdateRowWidths();
        }

        private void ClampToWorkingArea()
        {
            if (!Visible)
            {
                return;
            }

            Rectangle area = Screen.FromControl(this).WorkingArea;

            if (Top + Height > area.Bottom - 8)
            {
                Top = Math.Max(area.Top + 8, area.Bottom - 8 - Height);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (_list != null && _viewAll != null)
            {
                LayoutChrome();
                ClampToWorkingArea();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _manager.Changed -= Manager_Changed;
                ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
            }

            base.Dispose(disposing);
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;

            _title.ForeColor = palette.TextPrimary;
            _title.BackColor = palette.WindowBackground;
            _empty.ForeColor = palette.TextDisabled;
            _empty.BackColor = palette.WindowBackground;
            _list.BackColor = palette.WindowBackground;

            _clearButton.UseVisualStyleBackColor = false;
            _clearButton.BackColor = palette.WindowBackground;
            _clearButton.ForeColor = palette.TextSecondary;
            _clearButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            _clearButton.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;

            _closeButton.UseVisualStyleBackColor = false;
            _closeButton.BackColor = palette.WindowBackground;
            _closeButton.ForeColor = palette.TextSecondary;
            _closeButton.FlatAppearance.MouseOverBackColor = palette.CloseHover;
            _closeButton.FlatAppearance.MouseDownBackColor = palette.CloseHover;

            _viewAll.UseVisualStyleBackColor = false;
            _viewAll.BackColor = palette.WindowBackground;
            _viewAll.ForeColor = palette.TextPrimary;
            _viewAll.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            _viewAll.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;

            foreach (DownloadRow row in _rows.Values)
            {
                row.ApplyTheme();
            }
        }

        private int RowWidth()
        {
            int width = _list.ClientSize.Width - 4;

            if (_list.VerticalScroll.Visible)
            {
                width -= SystemInformation.VerticalScrollBarWidth;
            }

            return Math.Max(240, width);
        }

        private void UpdateRowWidths()
        {
            int width = RowWidth();

            foreach (DownloadRow row in _rows.Values)
            {
                row.Width = width;
            }
        }

        private void Manager_Changed(object sender, EventArgs e)
        {
            RefreshItems();
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (Pen pen = new Pen(ThemeManager.Current.AddressBorder))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    /// <summary>
    /// 单条下载记录：文件名、状态、进度条和操作按钮。
    /// 依赖：DownloadManager / ThemeManager。
    /// </summary>
    internal sealed class DownloadRow : UserControl
    {
        private readonly DownloadManager _manager;
        private readonly DownloadItem _item;
        private readonly Label _name;
        private readonly Label _status;
        private readonly ProgressLine _progress;
        private readonly FlowLayoutPanel _buttons;
        private readonly ToolTip _toolTip;
        private readonly GlyphButton _open;
        private readonly GlyphButton _folder;
        private readonly GlyphButton _pause;
        private readonly GlyphButton _cancel;
        private readonly GlyphButton _delete;
        private readonly GlyphButton _retry;

        public DownloadRow(DownloadManager manager, DownloadItem item)
        {
            _manager = manager;
            _item = item;

            Height = 88;
            Margin = new Padding(0, 0, 0, 6);
            BackColor = ThemeManager.Current.ChromeBackground;

            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);

            _name = new Label();
            _name.AutoSize = false;
            _name.AutoEllipsis = true;
            _name.Font = ThemeFonts.Ui(9.5F);
            _name.SetBounds(10, 8, 300, 20);

            _status = new Label();
            _status.AutoSize = false;
            _status.AutoEllipsis = true;
            _status.Font = ThemeFonts.Ui(8.5F);
            _status.SetBounds(10, 28, 300, 16);

            _progress = new ProgressLine();
            _progress.SetBounds(10, 48, 300, 4);

            _buttons = new FlowLayoutPanel();
            _buttons.FlowDirection = FlowDirection.RightToLeft;
            _buttons.WrapContents = false;
            _buttons.Padding = Padding.Empty;
            _buttons.Margin = Padding.Empty;
            _buttons.SetBounds(10, 58, 300, 22);

            _toolTip = new ToolTip();
            ThemeManager.ApplyToolTip(_toolTip);

            _open = MakeIconButton(IconFont.OpenFile, "打开文件");
            _folder = MakeIconButton(IconFont.OpenFolder, "打开所在文件夹");
            _pause = MakeIconButton(IconFont.Pause, "暂停");
            _cancel = MakeIconButton(IconFont.Cancel, "取消下载");
            _delete = MakeIconButton(IconFont.Delete, "删除记录");
            _retry = MakeIconButton(IconFont.Retry, "重试");

            _open.Click += delegate { _manager.OpenFile(_item); };
            _folder.Click += delegate { _manager.OpenFolder(_item); };
            _pause.Click += delegate
            {
                if (_item.Paused)
                {
                    _manager.Resume(_item);
                }
                else
                {
                    _manager.Pause(_item);
                }
            };
            _cancel.Click += delegate { _manager.Cancel(_item); };
            _delete.Click += delegate { _manager.Delete(_item); };
            _retry.Click += delegate { RaiseRetry(); };

            _buttons.Controls.Add(_cancel);
            _buttons.Controls.Add(_pause);
            _buttons.Controls.Add(_retry);
            _buttons.Controls.Add(_delete);
            _buttons.Controls.Add(_folder);
            _buttons.Controls.Add(_open);

            Controls.Add(_name);
            Controls.Add(_status);
            Controls.Add(_progress);
            Controls.Add(_buttons);

            Resize += delegate { LayoutRow(); };
            _item.Updated += Item_Updated;

            ApplyTheme();
            UpdateItem(item);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _item.Updated -= Item_Updated;
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 重试：交给 MainForm 打开新标签，让 WebView2 重新发起下载。
        /// </summary>
        public event EventHandler RetryRequested;

        private void RaiseRetry()
        {
            EventHandler handler = RetryRequested;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void UpdateItem(DownloadItem item)
        {
            _name.Text = item.DisplayName;
            _status.Text = Describe(item);
            _progress.Value = item.IsCompleted ? 1.0 : item.Progress;

            bool active = item.IsActive;
            bool completed = item.IsCompleted;
            bool failed = item.IsFailed;
            bool hasFile = !string.IsNullOrEmpty(item.FilePath);

            _progress.Visible = active;
            _open.Visible = completed;
            _open.Enabled = completed && File.Exists(item.FilePath);
            _folder.Visible = completed;
            _folder.Enabled = completed && hasFile;
            _pause.Visible = active;
            _pause.Glyph = item.Paused ? IconFont.Play : IconFont.Pause;
            _toolTip.SetToolTip(_pause, item.Paused ? "继续" : "暂停");
            _cancel.Visible = active;
            _delete.Visible = completed || failed;
            _retry.Visible = failed;

            _status.ForeColor = item.IsFailed
                ? Color.FromArgb(217, 83, 79)
                : ThemeManager.Current.TextSecondary;

            LayoutRow();
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.ChromeBackground;

            _name.ForeColor = palette.TextPrimary;
            _name.BackColor = palette.ChromeBackground;
            _status.ForeColor = _item.IsFailed ? Color.FromArgb(217, 83, 79) : palette.TextSecondary;
            _status.BackColor = palette.ChromeBackground;
            _progress.BackColor = palette.ChromeBackground;
            _progress.ForeColor = palette.Accent;
            _buttons.BackColor = palette.ChromeBackground;

            GlyphButton[] buttons = { _open, _folder, _pause, _cancel, _delete, _retry };

            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].UseVisualStyleBackColor = false;
                buttons[i].BackColor = palette.ChromeBackground;
                buttons[i].ForeColor = buttons[i].Enabled ? palette.TextPrimary : palette.TextDisabled;
                buttons[i].FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                buttons[i].FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            }

            Invalidate();
        }

        private void LayoutRow()
        {
            int width = Math.Max(20, Width - 20);
            _name.Width = width;
            _status.Width = width;
            _progress.Width = width;
            _buttons.Width = width;
            _buttons.Left = 10;
            _buttons.Top = Height - 30;
        }

        private void Item_Updated(object sender, EventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { UpdateItem(_item); }));
                return;
            }

            UpdateItem(_item);
        }

        private GlyphButton MakeIconButton(string glyph, string toolTip)
        {
            GlyphButton button = new GlyphButton(glyph);
            button.Width = 26;
            button.Height = 22;
            button.Font = IconFont.Create(9F);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TabStop = false;
            button.Cursor = Cursors.Default;
            button.Margin = new Padding(2, 0, 0, 0);
            _toolTip.SetToolTip(button, toolTip);
            return button;
        }

        private static string Describe(DownloadItem item)
        {
            if (item.IsCompleted)
            {
                return "已完成  " + FormatSize(item.TotalBytes > 0 ? item.TotalBytes : item.BytesReceived);
            }

            if (string.Equals(item.State, DownloadItem.StatePaused, StringComparison.Ordinal))
            {
                return "已暂停  " + FormatProgress(item);
            }

            if (string.Equals(item.State, DownloadItem.StateCancelled, StringComparison.Ordinal))
            {
                return "已取消";
            }

            if (item.IsFailed)
            {
                return string.IsNullOrEmpty(item.Error) ? "已中断" : "已中断  " + item.Error;
            }

            return "下载中  " + FormatProgress(item);
        }

        private static string FormatProgress(DownloadItem item)
        {
            if (item.TotalBytes > 0)
            {
                int percent = (int)Math.Round(item.Progress * 100);
                return FormatSize(item.BytesReceived) + " / " + FormatSize(item.TotalBytes) + " (" + percent + "%)";
            }

            return FormatSize(item.BytesReceived);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? ((long)value) + " " + units[unit]
                : value.ToString("0.0") + " " + units[unit];
        }
    }

    /// <summary>
    /// 细进度条：不依赖系统 ProgressBar，主题色可控。
    /// </summary>
    internal sealed class ProgressLine : Control
    {
        private double _value;

        public ProgressLine()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);
            Height = 4;
        }

        public double Value
        {
            get { return _value; }
            set
            {
                double next = Math.Max(0, Math.Min(1.0, value));

                if (Math.Abs(next - _value) < 0.001)
                {
                    return;
                }

                _value = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (SolidBrush back = new SolidBrush(ThemeManager.Current.ButtonHover))
            {
                e.Graphics.FillRectangle(back, 0, 0, Width, Height);
            }

            int filled = (int)Math.Round(Width * _value);

            if (filled > 0)
            {
                using (SolidBrush fill = new SolidBrush(ForeColor))
                {
                    e.Graphics.FillRectangle(fill, 0, 0, filled, Height);
                }
            }
        }
    }
}
