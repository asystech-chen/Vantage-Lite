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

        private static int S(int value)
        {
            return WindowChrome.Scale(value);
        }

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

        /// <summary>
        /// 行内「打开来源页」：由宿主在新标签中打开下载地址。
        /// </summary>
        public event EventHandler<DownloadItem> SourceRequested;

        /// <summary>
        /// 行内「删除磁盘文件」：由宿主确认后删除。
        /// </summary>
        public event EventHandler<DownloadItem> DeleteFileRequested;

        public DownloadsForm(DownloadManager manager)
        {
            _manager = manager;
            _rows = new Dictionary<string, DownloadRow>(StringComparer.Ordinal);

            Text = "下载内容";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(S(340), S(380));
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
            _title.SetBounds(S(14), S(12), S(200), S(22));

            _clearButton = new FlatButton();
            _clearButton.Text = "清空已完成";
            _clearButton.Width = S(88);
            _clearButton.Height = S(22);
            _clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _clearButton.FlatStyle = FlatStyle.Flat;
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.Font = ThemeFonts.Ui(9F);
            _clearButton.TabStop = false;
            _clearButton.Cursor = Cursors.Default;
            _clearButton.Click += delegate { _manager.ClearFinished(); };
            _clearButton.SetBounds(ClientSize.Width - S(128), S(11), S(88), S(22));

            _closeButton = new FlatButton();
            _closeButton.Text = IconFont.Close;
            _closeButton.Width = S(24);
            _closeButton.Height = S(24);
            _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _closeButton.FlatStyle = FlatStyle.Flat;
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Font = IconFont.CreatePx(11f);
            _closeButton.TabStop = false;
            _closeButton.Cursor = Cursors.Default;
            _closeButton.Click += delegate { Close(); };
            _closeButton.SetBounds(ClientSize.Width - S(32), S(10), S(24), S(24));

            _list = new FlowLayoutPanel();
            _list.SetBounds(S(10), S(42), ClientSize.Width - S(20), ClientSize.Height - S(86));
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.Padding = Padding.Empty;
            _list.Margin = Padding.Empty;

            _viewAll = new FlatButton();
            _viewAll.Text = "查看所有下载";
            _viewAll.Height = S(26);
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
            _viewAll.SetBounds(S(10), ClientSize.Height - S(34), ClientSize.Width - S(20), S(26));

            _empty = new Label();
            _empty.Text = "本次会话暂无下载";
            _empty.AutoSize = false;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.SetBounds(S(10), S(120), ClientSize.Width - S(20), S(40));
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
                    row.SourceRequested += delegate
                    {
                        EventHandler<DownloadItem> handler = SourceRequested;

                        if (handler != null)
                        {
                            handler(this, item);
                        }
                    };
                    row.DeleteFileRequested += delegate
                    {
                        EventHandler<DownloadItem> handler = DeleteFileRequested;

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
                ? S(MinPopupHeight)
                : S(HeaderHeight) + count * S(RowHeight) + S(FooterHeight);
            int height = Math.Max(S(MinPopupHeight), Math.Min(S(MaxPopupHeight), desired));

            if (ClientSize.Height != height)
            {
                ClientSize = new Size(ClientSize.Width, height);
            }
        }

        private void LayoutChrome()
        {
            int width = Math.Max(S(120), ClientSize.Width - S(20));
            int listHeight = Math.Max(S(40), ClientSize.Height - S(HeaderHeight) - S(FooterHeight));

            _list.SetBounds(S(10), S(HeaderHeight), width, listHeight);
            _viewAll.SetBounds(S(10), ClientSize.Height - S(34), width, S(26));
            _empty.SetBounds(S(10), S(HeaderHeight) + Math.Max(0, (listHeight - S(40)) / 2), width, S(40));

            UpdateRowWidths();
        }

        private void ClampToWorkingArea()
        {
            if (!Visible)
            {
                return;
            }

            Rectangle area = Screen.FromControl(this).WorkingArea;

            if (Top + Height > area.Bottom - S(8))
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
        private readonly GlyphButton _copy;
        private readonly GlyphButton _source;
        private readonly GlyphButton _deleteFile;

        private static int S(int value)
        {
            return WindowChrome.Scale(value);
        }

        public DownloadRow(DownloadManager manager, DownloadItem item)
        {
            _manager = manager;
            _item = item;

            Height = S(88);
            Margin = new Padding(0, 0, 0, S(6));
            BackColor = ThemeManager.Current.ChromeBackground;

            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);

            _name = new Label();
            _name.AutoSize = false;
            _name.AutoEllipsis = true;
            _name.Font = ThemeFonts.Ui(9.5F);
            _name.SetBounds(S(10), S(8), S(300), S(20));

            _status = new Label();
            _status.AutoSize = false;
            _status.AutoEllipsis = true;
            _status.Font = ThemeFonts.Ui(8.5F);
            _status.SetBounds(S(10), S(28), S(300), S(16));

            _progress = new ProgressLine();
            _progress.SetBounds(S(10), S(48), S(300), S(4));

            _buttons = new FlowLayoutPanel();
            _buttons.FlowDirection = FlowDirection.RightToLeft;
            _buttons.WrapContents = false;
            _buttons.Padding = Padding.Empty;
            _buttons.Margin = Padding.Empty;
            _buttons.SetBounds(S(10), S(58), S(300), S(22));

            _toolTip = new ToolTip();
            ThemeManager.ApplyToolTip(_toolTip);

            _open = MakeIconButton(IconFont.OpenFile, "打开文件");
            _folder = MakeIconButton(IconFont.OpenFolder, "打开所在文件夹");
            _pause = MakeIconButton(IconFont.Pause, "暂停");
            _cancel = MakeIconButton(IconFont.Cancel, "取消下载");
            _delete = MakeIconButton(IconFont.Delete, "删除记录");
            _retry = MakeIconButton(IconFont.Retry, "重试");
            _copy = MakeIconButton(IconFont.Copy, "复制下载链接");
            _source = MakeIconButton(IconFont.Link, "打开来源页");
            _deleteFile = MakeIconButton(IconFont.Cancel, "删除磁盘文件");

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
            _copy.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(_item.Url))
                {
                    Clipboard.SetText(_item.Url);
                }
            };
            _source.Click += delegate { RaiseSource(); };
            _deleteFile.Click += delegate { RaiseDeleteFile(); };

            _buttons.Controls.Add(_cancel);
            _buttons.Controls.Add(_pause);
            _buttons.Controls.Add(_retry);
            _buttons.Controls.Add(_delete);
            _buttons.Controls.Add(_deleteFile);
            _buttons.Controls.Add(_folder);
            _buttons.Controls.Add(_open);
            _buttons.Controls.Add(_source);
            _buttons.Controls.Add(_copy);

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
        public event EventHandler SourceRequested;
        public event EventHandler DeleteFileRequested;

        private void RaiseRetry()
        {
            EventHandler handler = RetryRequested;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseSource()
        {
            EventHandler handler = SourceRequested;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseDeleteFile()
        {
            EventHandler handler = DeleteFileRequested;

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
            _copy.Visible = !string.IsNullOrWhiteSpace(item.Url);
            _source.Visible = completed || failed;
            _deleteFile.Visible = completed && hasFile && File.Exists(item.FilePath);

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

            GlyphButton[] buttons = { _open, _folder, _pause, _cancel, _delete, _retry, _copy, _source, _deleteFile };

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
            button.Width = S(26);
            button.Height = S(22);
            button.Font = IconFont.CreatePx(12f);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TabStop = false;
            button.Cursor = Cursors.Default;
            button.Margin = new Padding(S(2), 0, 0, 0);
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
            Height = WindowChrome.Scale(4);
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
