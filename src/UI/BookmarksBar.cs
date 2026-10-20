using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 书签工具栏：展示「书签工具栏」文件夹的内容，文件夹以菜单展开。
    /// 依赖：BookmarkStore / ThemeManager。改动注意：所有编辑操作直接落到 Store，然后触发 Changed。
    /// </summary>
    public sealed class BookmarksBar : UserControl
    {
        public event EventHandler<string> OpenRequested;
        public event EventHandler ManageRequested;
        public event EventHandler Changed;

        private readonly FlowLayoutPanel _panel;
        private readonly ToolTip _toolTip;
        private readonly List<Button> _buttons;
        private readonly Panel _dropLine;
        private BookmarkStore _store;
        private Label _hintLabel;
        private BookmarkNode _dragNode;
        private BookmarkNode _dropTarget;
        private string _dropMode;
        private Button _dropHighlight;
        private Point _dragStart;
        private bool _dragArmed;

        public BookmarksBar()
        {
            Dock = DockStyle.Top;
            Height = 30;
            Padding = new Padding(6, 2, 6, 2);

            _buttons = new List<Button>();
            _toolTip = new ToolTip();

            _panel = new FlowLayoutPanel();
            _panel.Dock = DockStyle.Fill;
            _panel.WrapContents = false;
            _panel.AutoScroll = false;
            _panel.FlowDirection = FlowDirection.LeftToRight;
            _panel.Padding = Padding.Empty;
            _panel.Margin = Padding.Empty;
            _panel.MouseUp += Bar_MouseUp;
            _panel.AllowDrop = true;
            _panel.DragOver += Panel_DragOver;
            _panel.DragDrop += Panel_DragDrop;
            MouseUp += Bar_MouseUp;

            _dropLine = new Panel();
            _dropLine.Height = 2;
            _dropLine.Visible = false;
            _dropLine.Enabled = false;
            _dropLine.BackColor = ThemeManager.Current.Accent;

            Controls.Add(_panel);
            Controls.Add(_dropLine);
            _dropLine.BringToFront();

            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
                _toolTip.Dispose();
                ClearButtons();
            }

            base.Dispose(disposing);
        }

        public void Refresh(BookmarkStore store)
        {
            _store = store;
            ClearDropVisual();
            ClearButtons();

            if (_store == null || _store.ToolbarFolder == null)
            {
                return;
            }

            IList<BookmarkNode> children = _store.ToolbarFolder.GetChildren();

            if (children.Count == 0)
            {
                _hintLabel = new Label();
                _hintLabel.Text = "书签工具栏 · 按 Ctrl+D 添加书签";
                _hintLabel.AutoSize = true;
                _hintLabel.ForeColor = ThemeManager.Current.TextDisabled;
                _hintLabel.Font = ThemeFonts.Ui(9F);
                _hintLabel.Margin = new Padding(4, 6, 0, 0);
                _panel.Controls.Add(_hintLabel);
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                BookmarkNode node = children[i];
                Button button = CreateItemButton(node);
                _panel.Controls.Add(button);
                _buttons.Add(button);
            }
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.ChromeBackground;
            _panel.BackColor = palette.ChromeBackground;

            for (int i = 0; i < _buttons.Count; i++)
            {
                ApplyButtonTheme(_buttons[i]);
            }
        }

        private Button CreateItemButton(BookmarkNode node)
        {
            string text = node.IsFolder ? node.Title + " ▾" : node.Title;
            Button button = new Button();
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(4, 0, 4, 0);
            button.Height = 24;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TabStop = false;
            button.Cursor = Cursors.Default;
            button.Font = ThemeFonts.Ui(9.5F);

            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;

            int iconSpace = 22;

            // 宽度上限至少能完整放下「新建文件夹 ▾」，高 DPI 下也不会被截成「11…」。
            int folderReference = TextRenderer.MeasureText("新建文件夹 ▾", button.Font).Width + 24 + iconSpace;
            int maxWidth = Math.Max(242, folderReference);

            // AutoEllipsis 会额外弹出“完整文本”系统提示，和自定义 ToolTip 重叠；
            // 这里手动截断，保证只有一个提示来源。
            text = Shorten(text, button.Font, maxWidth - 16 - iconSpace);
            button.Text = text;

            int width = TextRenderer.MeasureText(text, button.Font).Width + 20 + iconSpace;
            button.Width = Math.Max(66, Math.Min(maxWidth, width));

            if (node.IsFolder)
            {
                button.Image = FolderIcon();
            }
            else
            {
                button.Image = GlobeIcon();

                Image cached = FaviconCache.GetOrLoad(node.Url, delegate (Image image)
                {
                    if (image == null)
                    {
                        return;
                    }

                    try
                    {
                        button.BeginInvoke(new Action(delegate
                        {
                            if (!button.IsDisposed)
                            {
                                button.Image = image;
                            }
                        }));
                    }
                    catch
                    {
                    }
                });

                if (cached != null)
                {
                    button.Image = cached;
                }
            }

            if (node.IsFolder)
            {
                button.Click += delegate { ShowFolderMenu(button, node); };
                _toolTip.SetToolTip(button, string.Empty);
            }
            else
            {
                string url = node.Url;
                button.Click += delegate { RaiseOpen(url); };
                _toolTip.SetToolTip(button, node.Title + "\n" + url);
            }

            button.MouseUp += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragArmed = false;
                    _dragNode = null;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    ShowItemMenu(button, node);
                }
            };

            button.AllowDrop = true;
            button.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragNode = node;
                    _dragStart = e.Location;
                    _dragArmed = true;
                }
            };
            button.MouseMove += delegate (object sender, MouseEventArgs e)
            {
                if (!_dragArmed || e.Button != MouseButtons.Left)
                {
                    return;
                }

                if (Math.Abs(e.X - _dragStart.X) < 4 && Math.Abs(e.Y - _dragStart.Y) < 4)
                {
                    return;
                }

                _dragArmed = false;
                button.DoDragDrop(node, DragDropEffects.Move);
                _dragNode = null;
                ClearDropVisual();
            };
            button.DragEnter += delegate (object sender, DragEventArgs e)
            {
                if (_dragNode != null)
                {
                    e.Effect = DragDropEffects.Move;
                }
            };
            button.DragOver += delegate (object sender, DragEventArgs e)
            {
                ButtonDragOver(button, node, e);
            };
            button.DragLeave += delegate { ClearDropVisual(); };
            button.DragDrop += delegate (object sender, DragEventArgs e)
            {
                ButtonDrop(node);
            };

            ApplyButtonTheme(button);
            return button;
        }

        private static Image _folderIcon;
        private static Image _globeIcon;

        private static Image FolderIcon()
        {
            if (_folderIcon == null)
            {
                Bitmap bitmap = new Bitmap(16, 16);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Font font = IconFont.Create(12F))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(214, 170, 60)))
                {
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    graphics.DrawString(IconFont.Folder, font, brush, -1, -2);
                }

                _folderIcon = bitmap;
            }

            return _folderIcon;
        }

        private static Image GlobeIcon()
        {
            if (_globeIcon == null)
            {
                Bitmap bitmap = new Bitmap(16, 16);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Pen pen = new Pen(Color.FromArgb(140, 146, 156), 1.2F))
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.DrawEllipse(pen, 2, 2, 12, 12);
                    graphics.DrawLine(pen, 2, 8, 14, 8);
                    graphics.DrawEllipse(pen, 6, 2, 4, 12);
                }

                _globeIcon = bitmap;
            }

            return _globeIcon;
        }

        private static string Shorten(string text, Font font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || TextRenderer.MeasureText(text, font).Width <= maxWidth)
            {
                return text ?? string.Empty;
            }

            string value = text;

            while (value.Length > 1 &&
                   TextRenderer.MeasureText(value + "…", font).Width > maxWidth)
            {
                value = value.Substring(0, value.Length - 1);
            }

            return value + "…";
        }

        private void ShowFolderMenu(Button button, BookmarkNode folder)
        {
            ContextMenuStrip menu = NewMenu();
            IList<BookmarkNode> children = folder.GetChildren();

            if (children.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("（空文件夹）");
                empty.Enabled = false;
                menu.Items.Add(empty);
            }
            else
            {
                for (int i = 0; i < children.Count; i++)
                {
                    menu.Items.Add(CreateMenuItem(children[i]));
                }
            }

            ShowMenuAtButton(button, menu);
        }

        private ToolStripMenuItem CreateMenuItem(BookmarkNode node)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(node.Title);
            item.ToolTipText = node.IsFolder ? null : node.Url;
            item.Tag = node;

            if (node.IsFolder)
            {
                IList<BookmarkNode> children = node.GetChildren();
                for (int i = 0; i < children.Count; i++)
                {
                    item.DropDownItems.Add(CreateMenuItem(children[i]));
                }

                item.Enabled = item.DropDownItems.Count > 0;
            }
            else
            {
                string url = node.Url;
                item.Click += delegate { RaiseOpen(url); };
            }

            return item;
        }

        private void ShowItemMenu(Button button, BookmarkNode node)
        {
            ContextMenuStrip menu = NewMenu();

            if (node.IsFolder)
            {
                menu.Items.Add(MenuItem("新建文件夹…", delegate { NewFolder(node.Id); }));
                menu.Items.Add(MenuItem("粘贴", delegate { Paste(node.Id); }, _store.ClipboardId != null));
            }
            else
            {
                menu.Items.Add(MenuItem("打开", delegate { RaiseOpen(node.Url); }));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("剪切", delegate { Cut(node.Id); }));
            menu.Items.Add(MenuItem("复制", delegate { CopyItem(node.Id); }));
            menu.Items.Add(BuildMoveMenu(node));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("重命名…", delegate { Rename(node); }));
            menu.Items.Add(MenuItem("删除", delegate { Delete(node); }));

            ShowMenu(button, menu);
        }

        private ToolStripMenuItem BuildMoveMenu(BookmarkNode node)
        {
            ToolStripMenuItem root = new ToolStripMenuItem("移动到文件夹");
            List<BookmarkNode> folders = new List<BookmarkNode>();
            CollectFolders(_store.Root, folders);

            bool added = false;

            for (int i = 0; i < folders.Count; i++)
            {
                BookmarkNode folder = folders[i];

                if (folder.Id == BookmarkStore.RootId ||
                    (node.IsFolder && _store.IsDescendantOrSelf(node.Id, folder.Id)))
                {
                    continue;
                }

                string id = folder.Id;
                string label = folder.Title;
                ToolStripMenuItem item = MenuItem(label, delegate { MoveNode(node.Id, id); });
                root.DropDownItems.Add(item);
                added = true;
            }

            root.Enabled = added;
            return root;
        }

        private void CollectFolders(BookmarkNode node, List<BookmarkNode> folders)
        {
            if (node == null || node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (child.IsFolder)
                {
                    folders.Add(child);
                    CollectFolders(child, folders);
                }
            }
        }

        private void ShowBarMenu()
        {
            ContextMenuStrip menu = NewMenu();
            menu.Items.Add(MenuItem("管理书签", delegate { Raise(ManageRequested); }));
            menu.Items.Add(MenuItem("新建文件夹…", delegate { NewFolder(BookmarkStore.ToolbarId); }));
            menu.Items.Add(MenuItem("粘贴", delegate { Paste(BookmarkStore.ToolbarId); }, _store.ClipboardId != null));
            ShowMenu(this, menu);
        }

        private void NewFolder(string parentId)
        {
            string name = PromptDialog.Ask(this, "新建文件夹", "文件夹名称：", "新建文件夹");
            if (!string.IsNullOrWhiteSpace(name))
            {
                _store.AddFolder(parentId, name);
                NotifyChanged();
            }
        }

        private void Rename(BookmarkNode node)
        {
            if (node.Id == BookmarkStore.ToolbarId || node.Id == BookmarkStore.OtherId)
            {
                return;
            }

            string name = PromptDialog.Ask(this, "重命名", "新名称：", node.Title);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _store.Rename(node.Id, name);
                NotifyChanged();
            }
        }

        private void Delete(BookmarkNode node)
        {
            if (node.Id == BookmarkStore.ToolbarId || node.Id == BookmarkStore.OtherId)
            {
                return;
            }

            string message = node.IsFolder
                ? "删除文件夹「" + node.Title + "」及其中的全部内容？"
                : "删除书签「" + node.Title + "」？";

            if (MessageBox.Show(this, message, "书签工具栏", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _store.Remove(node.Id);
                NotifyChanged();
            }
        }

        private void Cut(string id)
        {
            _store.Cut(id);
            NotifyChanged();
        }

        private void CopyItem(string id)
        {
            _store.CopyToClipboard(id);
            NotifyChanged();
        }

        private void Paste(string parentId)
        {
            _store.Paste(parentId);
            NotifyChanged();
        }

        private void MoveNode(string id, string parentId)
        {
            _store.Move(id, parentId);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            Refresh(_store);
            Raise(Changed);
        }

        private ContextMenuStrip NewMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = ThemeFonts.Ui(9.5F);
            return menu;
        }

        private void ShowMenu(Control owner, ContextMenuStrip menu)
        {
            PrepareMenu(menu);
            menu.Show(Cursor.Position);
        }

        private void ShowMenuAtButton(Control button, ContextMenuStrip menu)
        {
            PrepareMenu(menu);
            menu.Show(button, new Point(0, button.Height));
        }

        private void PrepareMenu(ContextMenuStrip menu)
        {
            menu.Closed += delegate
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(menu.Dispose));
                        return;
                    }
                    catch
                    {
                    }
                }

                menu.Dispose();
            };
        }

        private ToolStripMenuItem MenuItem(string text, Action action)
        {
            return MenuItem(text, action, true);
        }

        private ToolStripMenuItem MenuItem(string text, Action action, bool enabled)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Enabled = enabled;
            item.Click += delegate { action(); };
            return item;
        }

        private void ApplyButtonTheme(Button button)
        {
            ThemePalette palette = ThemeManager.Current;
            button.BackColor = palette.ChromeBackground;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                _panel.Controls.Remove(_buttons[i]);
                _buttons[i].Dispose();
            }

            _buttons.Clear();

            if (_hintLabel != null)
            {
                _panel.Controls.Remove(_hintLabel);
                _hintLabel.Dispose();
                _hintLabel = null;
            }
        }

        private void Panel_DragOver(object sender, DragEventArgs e)
        {
            if (_dragNode == null)
            {
                return;
            }

            e.Effect = DragDropEffects.Move;
            ClearDropVisual();
        }

        private void Panel_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragNode == null || _store == null)
            {
                return;
            }

            _store.Move(_dragNode.Id, BookmarkStore.ToolbarId);
            _dragNode = null;
            ClearDropVisual();
            NotifyChanged();
        }

        private void ButtonDragOver(Button button, BookmarkNode node, DragEventArgs e)
        {
            if (_dragNode == null || node == null ||
                string.Equals(_dragNode.Id, node.Id, StringComparison.Ordinal))
            {
                e.Effect = DragDropEffects.None;
                ClearDropVisual();
                return;
            }

            e.Effect = DragDropEffects.Move;

            Point point = button.PointToClient(new Point(e.X, e.Y));
            double ratio = button.Height <= 0 ? 0.5 : (double)point.Y / button.Height;
            string mode = node.IsFolder && ratio > 0.3 && ratio < 0.7
                ? "into"
                : (ratio < 0.5 ? "before" : "after");

            _dropTarget = node;
            _dropMode = mode;

            if (mode == "into")
            {
                ShowHighlight(button);
            }
            else
            {
                HideHighlight();
                ShowDropLine(button, mode == "before");
            }
        }

        private void ButtonDrop(BookmarkNode target)
        {
            if (_dragNode == null || _dropTarget == null || _store == null)
            {
                ClearDropVisual();
                return;
            }

            string mode = _dropMode;
            string dragId = _dragNode.Id;
            _dragNode = null;
            ClearDropVisual();

            if (mode == "into")
            {
                _store.Move(dragId, target.Id);
            }
            else
            {
                BookmarkNode parent = _store.ParentOf(target.Id);
                if (parent == null)
                {
                    return;
                }

                string before = mode == "before" ? target.Id : NextSiblingId(parent, target.Id);
                _store.Move(dragId, parent.Id, before);
            }

            NotifyChanged();
        }

        private string NextSiblingId(BookmarkNode parent, string id)
        {
            IList<BookmarkNode> children = parent.GetChildren();

            for (int i = 0; i < children.Count; i++)
            {
                if (string.Equals(children[i].Id, id, StringComparison.Ordinal))
                {
                    return i + 1 < children.Count ? children[i + 1].Id : null;
                }
            }

            return null;
        }

        private void ShowDropLine(Button button, bool before)
        {
            Point screen = _panel.PointToScreen(new Point(
                button.Left,
                before ? button.Top : button.Bottom - 2));
            Point local = PointToClient(screen);

            _dropLine.SetBounds(local.X, local.Y, button.Width, 2);
            _dropLine.BackColor = ThemeManager.Current.Accent;
            _dropLine.Visible = true;
            _dropLine.BringToFront();
        }

        private void ShowHighlight(Button button)
        {
            HideDropLine();

            if (_dropHighlight != null && _dropHighlight != button)
            {
                ApplyButtonTheme(_dropHighlight);
            }

            _dropHighlight = button;
            button.BackColor = ThemeManager.Current.ButtonHover;
        }

        private void HideHighlight()
        {
            if (_dropHighlight != null)
            {
                ApplyButtonTheme(_dropHighlight);
                _dropHighlight = null;
            }
        }

        private void HideDropLine()
        {
            if (_dropLine != null)
            {
                _dropLine.Visible = false;
            }
        }

        private void ClearDropVisual()
        {
            HideDropLine();
            HideHighlight();
            _dropTarget = null;
            _dropMode = null;
        }

        private void Bar_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _store != null)
            {
                ShowBarMenu();
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

        private void RaiseOpen(string url)
        {
            EventHandler<string> handler = OpenRequested;
            if (handler != null && !string.IsNullOrEmpty(url))
            {
                handler(this, url);
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

    /// <summary>
    /// 站点 favicon 内存缓存：按 host 拉取一次 /favicon.ico，失败也缓存，避免反复请求。
    /// 依赖：无。改动注意：回调在线程池线程执行，调用方需要自行切回 UI 线程。
    /// </summary>
    internal static class FaviconCache
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Action<Image>>> Pending =
            new Dictionary<string, List<Action<Image>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> DataUris =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static int Version { get; private set; }

        public static string GetDataUri(string url)
        {
            string key = KeyFor(url);
            if (key == null)
            {
                return null;
            }

            lock (Gate)
            {
                string data;
                return DataUris.TryGetValue(key, out data) ? data : null;
            }
        }

        public static Image GetOrLoad(string url, Action<Image> onLoaded)
        {
            string key = KeyFor(url);
            if (key == null)
            {
                return null;
            }

            lock (Gate)
            {
                Image cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                List<Action<Image>> callbacks;
                if (Pending.TryGetValue(key, out callbacks))
                {
                    if (onLoaded != null)
                    {
                        callbacks.Add(onLoaded);
                    }

                    return null;
                }

                callbacks = new List<Action<Image>>();
                if (onLoaded != null)
                {
                    callbacks.Add(onLoaded);
                }

                Pending[key] = callbacks;
            }

            ThreadPool.QueueUserWorkItem(delegate { Load(key, url); });
            return null;
        }

        private static void Load(string key, string pageUrl)
        {
            Image image = Download("https://" + key + "/favicon.ico");

            // 很多站点 favicon 不在根目录，回退到页面里的 <link rel="icon">。
            if (image == null)
            {
                string iconUrl = FindIconUrl(pageUrl);
                if (iconUrl != null)
                {
                    image = Download(iconUrl);
                }
            }

            string dataUri = null;

            if (image != null)
            {
                try
                {
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        image.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
                        dataUri = "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray());
                    }
                }
                catch
                {
                }
            }

            List<Action<Image>> callbacks;

            lock (Gate)
            {
                Cache[key] = image;
                DataUris[key] = dataUri;

                if (dataUri != null)
                {
                    Version++;
                }

                Pending.TryGetValue(key, out callbacks);
                Pending.Remove(key);
            }

            if (callbacks != null)
            {
                for (int i = 0; i < callbacks.Count; i++)
                {
                    try
                    {
                        callbacks[i](image);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static Image Download(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.UserAgent = "VantageLite";
                request.AllowAutoRedirect = true;

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    buffer.Position = 0;

                    using (Image decoded = Image.FromStream(buffer))
                    {
                        return Resize(decoded, 16, 16);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image Resize(Image source, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }

            return bitmap;
        }

        private static string FindIconUrl(string pageUrl)
        {
            string html = DownloadHtml(pageUrl);

            if (string.IsNullOrEmpty(html))
            {
                return null;
            }

            int index = 0;

            while ((index = html.IndexOf("<link", index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = html.IndexOf('>', index);
                if (end < 0)
                {
                    break;
                }

                string tag = html.Substring(index, end - index + 1);

                if (tag.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string href = ExtractAttribute(tag, "href");
                    Uri resolved;

                    if (!string.IsNullOrEmpty(href) &&
                        Uri.TryCreate(new Uri(pageUrl), href, out resolved))
                    {
                        return resolved.ToString();
                    }
                }

                index = end + 1;
            }

            return null;
        }

        private static string DownloadHtml(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.UserAgent = "VantageLite";
                request.AllowAutoRedirect = true;

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    char[] buffer = new char[262144];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    return new string(buffer, 0, read);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractAttribute(string tag, string name)
        {
            int index = tag.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            int start = index + name.Length + 1;
            if (start >= tag.Length)
            {
                return null;
            }

            char quote = tag[start];

            if (quote == '\'' || quote == '"')
            {
                start++;
                int end = tag.IndexOf(quote, start);
                return end > start ? tag.Substring(start, end - start) : null;
            }

            int stop = start;
            while (stop < tag.Length && !char.IsWhiteSpace(tag[stop]) && tag[stop] != '>')
            {
                stop++;
            }

            return stop > start ? tag.Substring(start, stop - start) : null;
        }

        private static string KeyFor(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return uri.Authority;
        }
    }
}
