using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VantageLite.App;
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
        private readonly Button _overflowButton;
        private readonly ToolTip _toolTip;
        private readonly List<Button> _buttons;
        private readonly List<BookmarkNode> _childNodes;
        private readonly List<BookmarkNode> _hiddenNodes;
        private readonly Panel _dropLine;
        private BookmarkStore _store;
        private Label _hintLabel;
        private BookmarkNode _dragNode;
        private BookmarkNode _dropTarget;
        private string _dropMode;
        private Button _dropHighlight;
        private ToolStripMenuItem _menuDropItem;
        private BookmarkNode _menuDropTarget;
        private string _menuDropMode;
        private readonly List<BookmarkDropPopup> _dropPopups = new List<BookmarkDropPopup>();
        private readonly System.Windows.Forms.Timer _dragTimer;
        private readonly System.Windows.Forms.Timer _relayoutTimer;
        private bool _manualDrag;
        private BookmarkNode _manualNode;
        private Button _manualButton;
        private BookmarkNode _manualTarget;
        private string _manualMode;
        private BookmarkDropPopup _manualPopup;
        private DragGhostForm _dragGhost;
        private bool _suppressClick;
        private bool _menuDragArmed;
        private BookmarkNode _menuDragNode;
        private Point _menuDragStart;
        private Point _dragStart;
        private bool _dragArmed;

        public BookmarksBar()
        {
            Dock = DockStyle.Top;
            Height = 30;
            Padding = new Padding(6, 2, 6, 2);
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);

            _buttons = new List<Button>();
            _childNodes = new List<BookmarkNode>();
            _hiddenNodes = new List<BookmarkNode>();
            _dragTimer = new System.Windows.Forms.Timer();
            _dragTimer.Interval = 40;
            _dragTimer.Tick += DragTimer_Tick;
            _relayoutTimer = new System.Windows.Forms.Timer();
            _relayoutTimer.Interval = 120;
            _relayoutTimer.Tick += delegate
            {
                _relayoutTimer.Stop();
                Relayout();
            };
            _toolTip = new ToolTip();
            ThemeManager.ApplyToolTip(_toolTip);

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
            _panel.SizeChanged += delegate
            {
                Relayout();
                ScheduleRelayout();
            };
            Resize += delegate { ScheduleRelayout(); };
            MouseUp += Bar_MouseUp;

            _overflowButton = new FlatButton();
            _overflowButton.Text = IconFont.More;
            _overflowButton.Dock = DockStyle.Right;
            _overflowButton.Width = 28;
            _overflowButton.FlatStyle = FlatStyle.Flat;
            _overflowButton.FlatAppearance.BorderSize = 0;
            _overflowButton.Font = IconFont.Create(10F);
            _overflowButton.TabStop = false;
            _overflowButton.Cursor = Cursors.Default;
            _overflowButton.Visible = false;
            _overflowButton.Click += delegate { ShowOverflowMenu(); };
            _overflowButton.AllowDrop = true;
            _overflowButton.DragEnter += Overflow_DragEnter;
            _overflowButton.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (_dragNode != null)
                {
                    e.Effect = DragDropEffects.Move;
                }
            };
            _overflowButton.DragDrop += Overflow_DragDrop;
            _toolTip.SetToolTip(_overflowButton, "显示放不下的书签");

            _dropLine = new Panel();
            _dropLine.Height = 2;
            _dropLine.Visible = false;
            _dropLine.Enabled = false;
            _dropLine.BackColor = ThemeManager.Current.Accent;

            Controls.Add(_panel);
            Controls.Add(_overflowButton);
            Controls.Add(_dropLine);
            _dropLine.BringToFront();

            ApplyTheme();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dragTimer.Stop();
                _dragTimer.Dispose();
                _relayoutTimer.Stop();
                _relayoutTimer.Dispose();
                CloseDragGhost();
                CloseDropPopups();
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
                ApplyTheme();
                return;
            }

            IList<BookmarkNode> children = _store.ToolbarFolder.GetChildren();
            _childNodes.Clear();

            for (int i = 0; i < children.Count; i++)
            {
                _childNodes.Add(children[i]);
            }

            if (children.Count == 0)
            {
                _hintLabel = new Label();
                _hintLabel.Text = "书签工具栏 · 按 Ctrl+D 添加书签";
                _hintLabel.AutoSize = true;
                _hintLabel.ForeColor = ThemeManager.Current.TextDisabled;
                _hintLabel.Font = ThemeFonts.Ui(9F);
                _hintLabel.Margin = new Padding(4, 6, 0, 0);
                _panel.Controls.Add(_hintLabel);
                _overflowButton.Visible = false;
                _hiddenNodes.Clear();
                ApplyTheme();
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                BookmarkNode node = children[i];
                Button button = CreateItemButton(node);
                _panel.Controls.Add(button);
                _buttons.Add(button);
            }

            ApplyTheme();
            Relayout();
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.ChromeBackground;
            _panel.BackColor = palette.ChromeBackground;

            if (_hintLabel != null && !_hintLabel.IsDisposed)
            {
                _hintLabel.ForeColor = palette.TextDisabled;
            }

            if (_dropLine != null && !_dropLine.IsDisposed)
            {
                _dropLine.BackColor = palette.Accent;
            }

            if (_overflowButton != null && !_overflowButton.IsDisposed)
            {
                ApplyButtonTheme(_overflowButton);
                _overflowButton.ForeColor = palette.TextSecondary;
                _overflowButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                _overflowButton.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                ApplyButtonTheme(_buttons[i]);
            }
        }

        private Button CreateItemButton(BookmarkNode node)
        {
            string text = node.IsFolder ? node.Title + " ▾" : node.Title;
            Button button = new FlatButton();
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(4, 0, 4, 0);
            button.Height = 24;
            button.Margin = new Padding(0, 0, 2, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
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
                button.Click += delegate
                {
                    if (_suppressClick) { _suppressClick = false; return; }
                    ShowFolderMenu(button, node);
                };
                _toolTip.SetToolTip(button, string.Empty);
            }
            else
            {
                string url = node.Url;
                button.Click += delegate
                {
                    if (_suppressClick) { _suppressClick = false; return; }
                    RaiseOpen(url);
                };
                _toolTip.SetToolTip(button, node.Title + "\n" + url);
            }

            button.MouseUp += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (_manualDrag)
                    {
                        FinishManualDrag();
                    }
                    else
                    {
                        _dragArmed = false;
                        _dragNode = null;
                    }
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
                    _suppressClick = false;
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

                StartManualDrag(button, node);
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
                // 优先用 Windows 资源管理器的文件夹图标，取不到再退回字体图标。
                _folderIcon = ShellIcons.Folder();

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
            ContextMenuStrip menu = NewMenu(true);
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
                    menu.Items.Add(CreateMenuItem(children[i], true, menu));
                }
            }

            ShowMenuAtButton(button, menu);
        }

        private ToolStripMenuItem CreateMenuItem(BookmarkNode node, bool showIcon, ContextMenuStrip ownerMenu)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(node.Title);
            item.ToolTipText = node.IsFolder ? null : node.Url;
            item.Tag = node;

            if (node.IsFolder)
            {
                IList<BookmarkNode> children = node.GetChildren();
                for (int i = 0; i < children.Count; i++)
                {
                    item.DropDownItems.Add(CreateMenuItem(children[i], showIcon, ownerMenu));
                }

                item.Enabled = item.DropDownItems.Count > 0;
                // 子菜单统一向左展开，避免跑到主窗口右侧外面。
                item.DropDownDirection = ToolStripDropDownDirection.Left;

                if (showIcon)
                {
                    item.Image = FolderIcon();
                }
            }
            else
            {
                string url = node.Url;
                item.Click += delegate { RaiseOpen(url); };

                if (showIcon)
                {
                    ApplyFavicon(item, url, ownerMenu);
                }
            }

            // 菜单里的项目也能拖回书签栏：菜单不支持 OLE 拖放，用鼠标移动手动接管。
            item.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _menuDragArmed = true;
                    _menuDragNode = node;
                    _menuDragStart = Cursor.Position;
                }
            };
            item.MouseMove += delegate
            {
                if (!_menuDragArmed || (Control.MouseButtons & MouseButtons.Left) == 0)
                {
                    return;
                }

                Point point = Cursor.Position;

                if (Math.Abs(point.X - _menuDragStart.X) < 4 &&
                    Math.Abs(point.Y - _menuDragStart.Y) < 4)
                {
                    return;
                }

                _menuDragArmed = false;
                MenuPositioner.CloseOpenMenus();
                StartManualDrag(null, _menuDragNode);
            };
            item.MouseUp += delegate
            {
                _menuDragArmed = false;
                _menuDragNode = null;
            };

            return item;
        }

        /// <summary>
        /// 菜单项图标异步加载：加载完成时菜单可能已经关闭，需要判 IsDisposed。
        /// </summary>
        private static void ApplyFavicon(ToolStripMenuItem item, string url, ContextMenuStrip ownerMenu)
        {
            Image cached = FaviconCache.GetOrLoad(url, delegate (Image image)
            {
                if (image == null)
                {
                    return;
                }

                try
                {
                    if (ownerMenu == null || ownerMenu.IsDisposed || !ownerMenu.IsHandleCreated)
                    {
                        return;
                    }

                    ownerMenu.BeginInvoke(new Action(delegate
                    {
                        if (!item.IsDisposed && !ownerMenu.IsDisposed)
                        {
                            item.Image = image;
                        }
                    }));
                }
                catch
                {
                }
            });

            if (cached != null)
            {
                item.Image = cached;
            }
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

            if (MessageDialog.Show(this, message, "书签工具栏", MessageBoxButtons.YesNo) == DialogResult.Yes)
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
            return NewMenu(false);
        }

        private ContextMenuStrip NewMenu(bool showIcons)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = showIcons;
            menu.ShowCheckMargin = false;
            menu.ImageScalingSize = new Size(16, 16);
            menu.Font = ThemeFonts.Ui(9.5F);
            ThemeManager.ApplyMenuTheme(menu);
            return menu;
        }

        private void ShowMenu(Control owner, ContextMenuStrip menu)
        {
            PrepareMenu(menu);
            MenuPositioner.Show(menu, this, Cursor.Position);
        }

        private void ShowMenuAtButton(Control button, ContextMenuStrip menu)
        {
            PrepareMenu(menu);
            MenuPositioner.Show(menu, this, button.PointToScreen(new Point(0, button.Height)));
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
            button.UseVisualStyleBackColor = false;
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
            _childNodes.Clear();
            _hiddenNodes.Clear();

            if (_overflowButton != null)
            {
                _overflowButton.Visible = false;
            }

            if (_hintLabel != null)
            {
                _panel.Controls.Remove(_hintLabel);
                _hintLabel.Dispose();
                _hintLabel = null;
            }
        }

        private void ScheduleRelayout()
        {
            _relayoutTimer.Stop();
            _relayoutTimer.Start();
        }

        /// <summary>
        /// 按当前宽度决定显示多少个书签，放不下的收进右侧「…」菜单。
        /// 窗口缩放、书签增删后都会重新分配。
        /// </summary>
        private void Relayout()
        {
            if (_panel == null || _overflowButton == null)
            {
                return;
            }

            if (_buttons.Count == 0)
            {
                _overflowButton.Visible = false;
                _hiddenNodes.Clear();
                return;
            }

            int available = _panel.ClientSize.Width;
            if (available <= 0)
            {
                return;
            }

            int total = 0;
            for (int i = 0; i < _buttons.Count; i++)
            {
                total += _buttons[i].Width + _buttons[i].Margin.Horizontal;
            }

            int fit;

            if (total <= available)
            {
                fit = _buttons.Count;
                _overflowButton.Visible = false;
            }
            else
            {
                int budget = Math.Max(0, available - _overflowButton.Width - 8);
                int used = 0;
                fit = 0;

                for (int i = 0; i < _buttons.Count; i++)
                {
                    int need = _buttons[i].Width + _buttons[i].Margin.Horizontal;

                    if (used + need > budget)
                    {
                        break;
                    }

                    used += need;
                    fit++;
                }

                // 窗口极窄时也至少保留一个，避免整条空白。
                if (fit == 0 && budget > 0)
                {
                    fit = 1;
                }

                _overflowButton.Visible = fit < _buttons.Count;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Visible = i < fit;
            }

            if (_overflowButton.Visible)
            {
                _overflowButton.BringToFront();
            }

            _hiddenNodes.Clear();

            for (int i = fit; i < _childNodes.Count && i < _buttons.Count; i++)
            {
                _hiddenNodes.Add(_childNodes[i]);
            }
        }

        /// <summary>
        /// 右侧「…」菜单：显示当前放不下的书签，样式与文件夹菜单一致。
        /// </summary>
        private void ShowOverflowMenu()
        {
            if (_hiddenNodes.Count == 0)
            {
                return;
            }

            ContextMenuStrip menu = NewMenu(true);

            for (int i = 0; i < _hiddenNodes.Count; i++)
            {
                menu.Items.Add(CreateMenuItem(_hiddenNodes[i], true, menu));
            }

            AttachMenuDrop(menu);
            ShowMenuAtButton(_overflowButton, menu);
        }

        /// <summary>
        /// 拖拽时用普通浮层代替 ContextMenuStrip：菜单不接收拖放，普通控件可以。
        /// </summary>
        private void ShowDropPopupsForOverflow()
        {
            CloseDropPopups();

            if (_hiddenNodes.Count == 0)
            {
                return;
            }

            BookmarkDropPopup popup = new BookmarkDropPopup(this, _hiddenNodes, BookmarkStore.ToolbarId, null);
            popup.ShowRightAligned(_overflowButton, FindForm());
            _dropPopups.Add(popup);
        }

        private void StartManualDrag(Button button, BookmarkNode node)
        {
            _dragArmed = false;
            _manualDrag = true;
            _manualNode = node;
            _manualButton = button;
            _dragNode = node;
            _suppressClick = true;

            if (button != null)
            {
                try
                {
                    button.Capture = true;
                }
                catch
                {
                }
            }

            _dragTimer.Start();

            if (button != null && !button.IsDisposed)
            {
                button.BackColor = ThemeManager.Current.ButtonHover;
            }

            ShowDragGhost(node.Title);
            Cursor = Cursors.SizeAll;
        }

        private void DragTimer_Tick(object sender, EventArgs e)
        {
            if (!_manualDrag)
            {
                _dragTimer.Stop();
                return;
            }

            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                FinishManualDrag();
                return;
            }

            UpdateDragGhost(Cursor.Position);
            UpdateManualHover(Cursor.Position);
        }

        private void FinishManualDrag()
        {
            if (!_manualDrag)
            {
                return;
            }

            _manualDrag = false;
            _dragTimer.Stop();

            if (_manualButton != null)
            {
                try
                {
                    _manualButton.Capture = false;
                }
                catch
                {
                }
            }

            BookmarkNode target = _manualTarget;
            string mode = _manualMode;

            if (_manualButton != null && !_manualButton.IsDisposed)
            {
                ApplyButtonTheme(_manualButton);
            }

            CloseDragGhost();
            Cursor = Cursors.Default;

            if (target != null && _dragNode != null && _store != null)
            {
                PerformMenuDrop(target, mode);
            }

            _dragNode = null;
            _manualNode = null;
            _manualButton = null;
            _manualTarget = null;
            _manualMode = null;
            _manualPopup = null;
            HideHighlight();
            HideDropLine();
            CloseDropPopups();
            ClearDropVisual();
            _suppressClick = true;
        }

        private void ShowDragGhost(string title)
        {
            CloseDragGhost();
            _dragGhost = new DragGhostForm(title);
            Form owner = FindForm();

            if (owner != null)
            {
                _dragGhost.Show(owner);
            }
            else
            {
                _dragGhost.Show();
            }

            UpdateDragGhost(Cursor.Position);
        }

        private void UpdateDragGhost(Point screen)
        {
            if (_dragGhost == null || _dragGhost.IsDisposed)
            {
                return;
            }

            Form owner = FindForm();
            Rectangle area = owner != null
                ? owner.RectangleToScreen(owner.ClientRectangle)
                : Screen.FromPoint(screen).WorkingArea;

            _dragGhost.KeepInside(screen, area);
        }

        private void CloseDragGhost()
        {
            if (_dragGhost != null)
            {
                try
                {
                    _dragGhost.Close();
                }
                catch
                {
                }

                _dragGhost = null;
            }
        }

        private void UpdateManualHover(Point screen)
        {
            if (_manualNode == null)
            {
                return;
            }

            // 悬停「…」：弹出可接收鼠标定位的自定义浮层。
            if (_overflowButton.Visible &&
                _overflowButton.RectangleToScreen(_overflowButton.ClientRectangle).Contains(screen))
            {
                if (_manualPopup == null || _manualPopup.IsDisposed)
                {
                    ShowDropPopupsForOverflow();
                    _manualPopup = _dropPopups.Count > 0 ? _dropPopups[0] : null;
                }

                _manualTarget = null;
                _manualMode = null;
                HideHighlight();
                HideDropLine();
                return;
            }

            if (_manualPopup != null && !_manualPopup.IsDisposed)
            {
                for (int i = _dropPopups.Count - 1; i >= 0; i--)
                {
                    BookmarkDropPopup popup = _dropPopups[i];

                    if (popup.IsDisposed)
                    {
                        continue;
                    }

                    BookmarkDropPopup.DropRow row = popup.RowAt(screen);

                    if (row != null)
                    {
                        if (string.Equals(row.Node.Id, _manualNode.Id, StringComparison.Ordinal))
                        {
                            _manualTarget = null;
                            _manualMode = null;
                            popup.ClearHover();
                            HideHighlight();
                            HideDropLine();
                            return;
                        }

                        string mode = popup.ModeAt(row, screen);
                        _manualTarget = row.Node;
                        _manualMode = mode;
                        popup.SetHover(row, mode);
                        HideHighlight();
                        HideDropLine();

                        if (mode == "into" && row.Node.IsFolder)
                        {
                            popup.OpenChildManual(row.Node, row);
                        }

                        return;
                    }
                }

                _manualTarget = null;
                _manualMode = null;
                _manualPopup.ClearHover();
            }

            // 书签栏本身。
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button button = _buttons[i];

                if (!button.Visible)
                {
                    continue;
                }

                Rectangle rect = button.RectangleToScreen(button.ClientRectangle);

                if (!rect.Contains(screen))
                {
                    continue;
                }

                BookmarkNode node = i < _childNodes.Count ? _childNodes[i] : null;

                if (node == null || string.Equals(node.Id, _manualNode.Id, StringComparison.Ordinal))
                {
                    _manualTarget = null;
                    _manualMode = null;
                    HideHighlight();
                    HideDropLine();
                    return;
                }

                double ratio = rect.Width <= 0 ? 0.5 : (double)(screen.X - rect.Left) / rect.Width;
                string mode = node.IsFolder && ratio > 0.3 && ratio < 0.7
                    ? "into"
                    : (ratio < 0.5 ? "before" : "after");

                _manualTarget = node;
                _manualMode = mode;

                if (mode == "into")
                {
                    ShowHighlight(button);
                }
                else
                {
                    HideHighlight();
                    ShowDropLine(button, mode == "before");
                }

                return;
            }

            _manualTarget = null;
            _manualMode = null;
            HideHighlight();
            HideDropLine();
        }

        private void ShowChildDropPopup(BookmarkDropPopup parent, BookmarkNode folder, Control anchor)
        {
            for (int i = _dropPopups.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_dropPopups[i].ParentPopup, parent))
                {
                    _dropPopups[i].Close();
                    _dropPopups.RemoveAt(i);
                }
            }

            IList<BookmarkNode> children = folder.GetChildren();

            if (children.Count == 0)
            {
                return;
            }

            BookmarkDropPopup child = new BookmarkDropPopup(this, children, folder.Id, parent);
            child.ShowLeftOf(parent, anchor, FindForm());
            _dropPopups.Add(child);
        }

        private void CloseDropPopups()
        {
            for (int i = _dropPopups.Count - 1; i >= 0; i--)
            {
                try
                {
                    _dropPopups[i].Close();
                }
                catch
                {
                }
            }

            _dropPopups.Clear();
        }

        private void PerformMenuDrop(BookmarkNode target, string mode)
        {
            if (_dragNode == null || _store == null || target == null)
            {
                return;
            }

            string id = _dragNode.Id;
            _dragNode = null;
            CloseDropPopups();
            ClearDropVisual();

            if (mode == "into")
            {
                _store.Move(id, target.Id);
            }
            else
            {
                BookmarkNode parent = _store.ParentOf(target.Id);

                if (parent == null)
                {
                    return;
                }

                string before = mode == "before"
                    ? target.Id
                    : NextSiblingId(parent, target.Id);

                _store.Move(id, parent.Id, before);
            }

            NotifyChanged();
        }

        /// <summary>
        /// 拖拽浮层：普通 Form + 普通按钮，能接收 DragEnter/Over/Drop；
        /// 文件夹行悬停时再弹一个同样结构的子浮层。
        /// </summary>
        /// <summary>
        /// 拖拽时跟随光标的标题小浮标，给用户明确的“已经拖起来了”反馈。
        /// </summary>
        internal sealed class DragGhostForm : Form
        {
            public DragGhostForm(string text)
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                BackColor = ThemeManager.Current.Accent;
                ForeColor = Color.White;
                Font = ThemeFonts.Ui(9F);

                int width = TextRenderer.MeasureText(text ?? string.Empty, Font).Width + 24;
                Width = Math.Max(80, Math.Min(280, width));
                Height = 26;
                Opacity = 0.92;

                Label label = new Label();
                label.Text = text ?? string.Empty;
                label.AutoSize = false;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Padding = new Padding(8, 0, 8, 0);
                label.BackColor = Color.Transparent;
                label.ForeColor = Color.White;
                label.AutoEllipsis = true;
                Controls.Add(label);
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            public void KeepInside(Point cursor, Rectangle area)
            {
                int x = cursor.X + 14;
                int y = cursor.Y + 14;

                if (x + Width > area.Right)
                {
                    x = cursor.X - Width - 10;
                }

                if (y + Height > area.Bottom)
                {
                    y = cursor.Y - Height - 10;
                }

                Left = Math.Max(area.Left, x);
                Top = Math.Max(area.Top, y);
            }
        }

        internal sealed class BookmarkDropPopup : Form
        {
            private readonly BookmarksBar _owner;
            private readonly FlowLayoutPanel _list;
            private BookmarkNode _openChildNode;

            internal BookmarkDropPopup(BookmarksBar owner, IList<BookmarkNode> nodes,
                string parentId, BookmarkDropPopup parentPopup)
            {
                _owner = owner;
                ParentPopup = parentPopup;

                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Width = 240;
                BackColor = ThemeManager.Current.WindowBackground;
                Height = Math.Min(360, 10 + nodes.Count * 30);

                _list = new FlowLayoutPanel();
                _list.Dock = DockStyle.Fill;
                _list.FlowDirection = FlowDirection.TopDown;
                _list.WrapContents = false;
                _list.AutoScroll = true;
                _list.Padding = new Padding(4);
                _list.BackColor = ThemeManager.Current.WindowBackground;
                Controls.Add(_list);

                for (int i = 0; i < nodes.Count; i++)
                {
                    _list.Controls.Add(new DropRow(this, nodes[i]));
                }
            }

            public BookmarkDropPopup ParentPopup { get; private set; }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            public void ShowRightAligned(Control anchor, Form owner)
            {
                Rectangle area = GetArea(anchor, owner);

                // 右侧钉死到主窗口右边缘，垂直方向以按钮下方为锚点，放不下就翻到上方。
                Left = Math.Max(area.Left + 4, area.Right - Width - 4);

                Point anchorScreen = anchor.PointToScreen(Point.Empty);
                int top = anchorScreen.Y + anchor.Height;

                if (top + Height > area.Bottom - 4)
                {
                    top = anchorScreen.Y - Height;
                }

                Top = Math.Max(area.Top + 4, Math.Min(top, area.Bottom - Height - 4));
                ShowOwned(owner);
            }

            public void ShowLeftOf(BookmarkDropPopup parent, Control anchor, Form owner)
            {
                Rectangle area = GetArea(anchor, owner);

                // 二级菜单只向左展开。
                Left = Math.Max(area.Left + 4, parent.Left - Width - 4);

                int top = anchor.PointToScreen(Point.Empty).Y;
                Top = Math.Max(area.Top + 4, Math.Min(top, area.Bottom - Height - 4));
                ShowOwned(owner);
            }

            private static Rectangle GetArea(Control anchor, Form owner)
            {
                return owner != null
                    ? owner.RectangleToScreen(owner.ClientRectangle)
                    : Screen.FromControl(anchor).WorkingArea;
            }

            private void ShowOwned(Form owner)
            {
                if (owner != null)
                {
                    Show(owner);
                }
                else
                {
                    Show();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                using (Pen pen = new Pen(ThemeManager.Current.AddressBorder))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            public void OpenChildManual(BookmarkNode folder, Control anchor)
            {
                OpenChild(folder, anchor);
            }

            public DropRow RowAt(Point screen)
            {
                for (int i = 0; i < _list.Controls.Count; i++)
                {
                    DropRow row = _list.Controls[i] as DropRow;

                    if (row != null && row.RectangleToScreen(row.ClientRectangle).Contains(screen))
                    {
                        return row;
                    }
                }

                return null;
            }

            public string ModeAt(DropRow row, Point screen)
            {
                Rectangle rect = row.RectangleToScreen(row.ClientRectangle);
                double ratio = rect.Height <= 0 ? 0.5 : (double)(screen.Y - rect.Top) / rect.Height;

                return row.Node.IsFolder && ratio > 0.3 && ratio < 0.7
                    ? "into"
                    : (ratio < 0.5 ? "before" : "after");
            }

            public void SetHover(DropRow row, string mode)
            {
                for (int i = 0; i < _list.Controls.Count; i++)
                {
                    DropRow current = _list.Controls[i] as DropRow;

                    if (current != null)
                    {
                        current.SetHover(ReferenceEquals(current, row) ? mode : null);
                    }
                }
            }

            public void ClearHover()
            {
                for (int i = 0; i < _list.Controls.Count; i++)
                {
                    DropRow row = _list.Controls[i] as DropRow;

                    if (row != null)
                    {
                        row.SetHover(null);
                    }
                }
            }

            private void OpenChild(BookmarkNode folder, Control anchor)
            {
                if (ReferenceEquals(_openChildNode, folder))
                {
                    return;
                }

                _openChildNode = folder;
                _owner.ShowChildDropPopup(this, folder, anchor);
            }

            private void Drop(BookmarkNode target, string mode)
            {
                _owner.PerformMenuDrop(target, mode);
            }

            private bool IsDragging
            {
                get { return _owner._dragNode != null; }
            }

            internal sealed class DropRow : FlatButton
            {
                private readonly BookmarkDropPopup _popup;

                internal DropRow(BookmarkDropPopup popup, BookmarkNode node)
                {
                    _popup = popup;
                    Node = node;

                    Text = node.Title + (node.IsFolder ? "  ▸" : string.Empty);
                    TextAlign = ContentAlignment.MiddleLeft;
                    Height = 26;
                    Width = popup.Width - 16;
                    Margin = new Padding(0, 0, 0, 2);
                    FlatStyle = FlatStyle.Flat;
                    FlatAppearance.BorderSize = 0;
                    Font = ThemeFonts.Ui(9.5F);
                    TabStop = false;
                    Cursor = Cursors.Default;
                    AllowDrop = true;
                    ImageAlign = ContentAlignment.MiddleLeft;
                    TextImageRelation = TextImageRelation.ImageBeforeText;

                    if (node.IsFolder)
                    {
                        Image = BookmarksBar.FolderIcon();
                    }
                    else
                    {
                        Image = FaviconCache.GetOrLoad(node.Url, null) ?? BookmarksBar.GlobeIcon();
                    }

                    ApplyRowTheme();

                    DragEnter += delegate(object sender, DragEventArgs e)
                    {
                        e.Effect = _popup.IsDragging ? DragDropEffects.Move : DragDropEffects.None;
                    };
                    DragOver += Row_DragOver;
                    DragLeave += delegate { ApplyRowTheme(); };
                    DragDrop += delegate(object sender, DragEventArgs e)
                    {
                        e.Effect = DragDropEffects.Move;
                        ApplyRowTheme();
                        _popup.Drop(Node, Mode);
                    };
                }

                public BookmarkNode Node { get; private set; }

                public string Mode { get; private set; }

                private string _lineMode;

                public void SetHover(string mode)
                {
                    _lineMode = (mode == "before" || mode == "after") ? mode : null;

                    if (mode == "into")
                    {
                        BackColor = ThemeManager.Current.ButtonHover;
                    }
                    else
                    {
                        ApplyRowTheme();
                    }

                    Invalidate();
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    base.OnPaint(e);

                    if (string.IsNullOrEmpty(_lineMode))
                    {
                        return;
                    }

                    using (Pen pen = new Pen(ThemeManager.Current.Accent, 2F))
                    {
                        int y = _lineMode == "before" ? 1 : Height - 2;
                        e.Graphics.DrawLine(pen, 2, y, Width - 2, y);
                    }
                }

                private void Row_DragOver(object sender, DragEventArgs e)
                {
                    if (!_popup.IsDragging ||
                        string.Equals(_popup._owner._dragNode.Id, Node.Id, StringComparison.Ordinal))
                    {
                        e.Effect = DragDropEffects.None;
                        ApplyRowTheme();
                        return;
                    }

                    Point client = PointToClient(new Point(e.X, e.Y));
                    double ratio = Height <= 0 ? 0.5 : (double)client.Y / Height;
                    Mode = Node.IsFolder && ratio > 0.3 && ratio < 0.7
                        ? "into"
                        : (ratio < 0.5 ? "before" : "after");

                    e.Effect = DragDropEffects.Move;
                    BackColor = ThemeManager.Current.ButtonHover;

                    if (Mode == "into")
                    {
                        _popup.OpenChild(Node, this);
                    }
                }

                private void ApplyRowTheme()
                {
                    ThemePalette palette = ThemeManager.Current;
                    BackColor = palette.WindowBackground;
                    ForeColor = palette.TextPrimary;
                    FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                    FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
                }
            }
        }

        /// <summary>
        /// 拖拽到「…」按钮时打开溢出菜单，并把菜单及所有子菜单做成放置目标。
        /// </summary>
        private void Overflow_DragEnter(object sender, DragEventArgs e)
        {
            if (_dragNode == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;
            ShowDropPopupsForOverflow();
        }

        private void Overflow_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragNode == null || _store == null)
            {
                ClearDropVisual();
                return;
            }

            string id = _dragNode.Id;
            _dragNode = null;
            _menuDropTarget = null;
            _menuDropMode = null;
            ClearDropVisual();
            _store.Move(id, BookmarkStore.ToolbarId);
            NotifyChanged();
        }

        private void AttachMenuDrop(ToolStripDropDown dropDown)
        {
            dropDown.AllowDrop = true;
            dropDown.DragEnter += Menu_DragEnter;
            dropDown.DragOver += Menu_DragOver;
            dropDown.DragLeave += Menu_DragLeave;
            dropDown.DragDrop += Menu_DragDrop;

            for (int i = 0; i < dropDown.Items.Count; i++)
            {
                ToolStripMenuItem item = dropDown.Items[i] as ToolStripMenuItem;

                if (item != null && item.DropDownItems.Count > 0)
                {
                    AttachMenuDrop(item.DropDown);
                }
            }
        }

        private void Menu_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = _dragNode == null ? DragDropEffects.None : DragDropEffects.Move;
        }

        private void Menu_DragOver(object sender, DragEventArgs e)
        {
            if (_dragNode == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            ToolStripDropDown dropDown = sender as ToolStripDropDown;
            ToolStripMenuItem item = MenuItemAt(sender, e);
            BookmarkNode node = item == null ? null : item.Tag as BookmarkNode;

            if (node == null || string.Equals(node.Id, _dragNode.Id, StringComparison.Ordinal))
            {
                e.Effect = DragDropEffects.None;
                SetMenuDropHighlight(null);
                return;
            }

            Point client = dropDown == null
                ? Point.Empty
                : dropDown.PointToClient(new Point(e.X, e.Y));
            double ratio = item.Height <= 0
                ? 0.5
                : (double)(client.Y - item.Bounds.Top) / item.Height;

            // 文件夹中间 = 放入；其余位置 = 插到目标前/后，用于自定义菜单内顺序。
            string mode = node.IsFolder && ratio > 0.3 && ratio < 0.7
                ? "into"
                : (ratio < 0.5 ? "before" : "after");

            e.Effect = DragDropEffects.Move;
            _menuDropTarget = node;
            _menuDropMode = mode;
            SetMenuDropHighlight(item);

            if (mode == "into" && !item.DropDown.Visible)
            {
                try
                {
                    item.ShowDropDown();
                }
                catch
                {
                }
            }
        }

        private void Menu_DragLeave(object sender, EventArgs e)
        {
            _menuDropTarget = null;
            _menuDropMode = null;
            SetMenuDropHighlight(null);
        }

        private void Menu_DragDrop(object sender, DragEventArgs e)
        {
            BookmarkNode target = _menuDropTarget;
            string mode = _menuDropMode;

            if (_dragNode == null || _store == null || target == null)
            {
                SetMenuDropHighlight(null);
                return;
            }

            string id = _dragNode.Id;
            _dragNode = null;
            _menuDropTarget = null;
            _menuDropMode = null;
            ClearDropVisual();
            SetMenuDropHighlight(null);
            MenuPositioner.CloseOpenMenus();

            if (mode == "into")
            {
                _store.Move(id, target.Id);
            }
            else
            {
                BookmarkNode parent = _store.ParentOf(target.Id);

                if (parent == null)
                {
                    return;
                }

                string before = mode == "before"
                    ? target.Id
                    : NextSiblingId(parent, target.Id);

                _store.Move(id, parent.Id, before);
            }

            NotifyChanged();
        }

        private static ToolStripMenuItem MenuItemAt(object sender, DragEventArgs e)
        {
            ToolStripDropDown dropDown = sender as ToolStripDropDown;

            if (dropDown == null)
            {
                return null;
            }

            return dropDown.GetItemAt(dropDown.PointToClient(new Point(e.X, e.Y))) as ToolStripMenuItem;
        }

        private void SetMenuDropHighlight(ToolStripMenuItem item)
        {
            if (ReferenceEquals(_menuDropItem, item))
            {
                return;
            }

            if (_menuDropItem != null)
            {
                _menuDropItem.BackColor = Color.Empty;
                _menuDropItem.ForeColor = ThemeManager.Current.TextPrimary;
            }

            _menuDropItem = item;

            if (item != null)
            {
                item.BackColor = ThemeManager.Current.ButtonHover;
                item.ForeColor = ThemeManager.Current.TextPrimary;
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

            // 书签栏是横向排列，左右位置才表示插入前后。
            Point point = button.PointToClient(new Point(e.X, e.Y));
            double ratio = button.Width <= 0 ? 0.5 : (double)point.X / button.Width;
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
            int x = before ? button.Left : button.Right - 2;

            Point screen = _panel.PointToScreen(new Point(x, button.Top));
            Point local = PointToClient(screen);

            _dropLine.SetBounds(local.X, local.Y, 2, button.Height);
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
    /// 右键菜单定位：所有菜单都限制在主窗口客户区内，避免跑到窗口外。
    /// 依赖：WinForms。改动注意：子菜单在 Opened 后修正位置，可能有一次位置跳动。
    /// </summary>
    internal static class MenuPositioner
    {
        private static readonly List<ToolStripDropDown> OpenMenus = new List<ToolStripDropDown>();
        private static System.Windows.Forms.Timer _mouseWatch;

        public static bool HasOpenMenu
        {
            get
            {
                for (int i = 0; i < OpenMenus.Count; i++)
                {
                    if (!OpenMenus[i].IsDisposed && OpenMenus[i].Visible)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool ContainsScreenPoint(Point point)
        {
            for (int i = 0; i < OpenMenus.Count; i++)
            {
                ToolStripDropDown menu = OpenMenus[i];

                if (!menu.IsDisposed && menu.Visible && menu.Bounds.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        public static void CloseOpenMenus()
        {
            ToolStripDropDown[] menus = OpenMenus.ToArray();

            for (int i = 0; i < menus.Length; i++)
            {
                try
                {
                    menus[i].Close();
                }
                catch
                {
                }
            }

            OpenMenus.Clear();
        }

        public static void Show(ContextMenuStrip menu, Control owner, Point screenPoint)
        {
            if (menu == null || owner == null)
            {
                return;
            }

            Control boundsOwner = owner.TopLevelControl ?? owner;
            ConstrainSubMenus(menu, boundsOwner);

            Rectangle bounds = boundsOwner.RectangleToScreen(boundsOwner.ClientRectangle);
            Size size = menu.GetPreferredSize(Size.Empty);

            int x = screenPoint.X;
            int y = screenPoint.Y;

            if (x + size.Width > bounds.Right)
            {
                x = bounds.Right - size.Width;
            }

            // 垂直方向也限制在主窗口客户区内；放不下时上移，避免菜单跑到窗口外。
            int maxY = Math.Max(bounds.Top, bounds.Bottom - size.Height);

            if (y > maxY)
            {
                y = maxY;
            }

            if (y < bounds.Top)
            {
                y = bounds.Top;
            }

            x = Math.Max(bounds.Left, x);

            Track(menu);
            menu.Closed += delegate { Untrack(menu); };
            menu.Opened += delegate { ClampDropDown(menu, boundsOwner); };
            StartMouseWatch();
            menu.Show(new Point(x, y));
        }

        private static void StartMouseWatch()
        {
            if (_mouseWatch == null)
            {
                _mouseWatch = new System.Windows.Forms.Timer();
                _mouseWatch.Interval = 15;
                _mouseWatch.Tick += delegate
                {
                    if (!HasOpenMenu)
                    {
                        _mouseWatch.Stop();
                        return;
                    }

                    // 低层消息可能被 WebView2 吞掉，这里直接看鼠标按键状态与光标位置。
                    if (Control.MouseButtons != MouseButtons.None &&
                        !ContainsScreenPoint(Cursor.Position))
                    {
                        CloseOpenMenus();
                    }
                };
            }

            _mouseWatch.Start();
        }

        private static void Track(ToolStripDropDown menu)
        {
            if (menu != null && !OpenMenus.Contains(menu))
            {
                OpenMenus.Add(menu);
            }
        }

        private static void Untrack(ToolStripDropDown menu)
        {
            OpenMenus.Remove(menu);
        }

        private static void ConstrainSubMenus(ContextMenuStrip menu, Control owner)
        {
            Attach(menu.Items, menu, owner);
        }

        private static void Attach(ToolStripItemCollection items, ContextMenuStrip menu, Control owner)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ToolStripMenuItem item = items[i] as ToolStripMenuItem;
                if (item == null || item.DropDownItems.Count == 0)
                {
                    continue;
                }

                ToolStripDropDown dropDown = item.DropDown;
                dropDown.Renderer = menu.Renderer;
                dropDown.BackColor = ThemeManager.Current.ChromeBackground;
                dropDown.ForeColor = ThemeManager.Current.TextPrimary;
                dropDown.Opened += delegate
                {
                    Track(dropDown);
                    PlaceSubMenuLeft(dropDown, item, owner);

                    // WinForms 在 Opened 之后还会再摆一次，延迟再校正一遍。
                    try
                    {
                        dropDown.BeginInvoke(new Action(delegate
                        {
                            if (!dropDown.IsDisposed && dropDown.Visible)
                            {
                                PlaceSubMenuLeft(dropDown, item, owner);
                            }
                        }));
                    }
                    catch
                    {
                    }
                };
                dropDown.Closed += delegate { Untrack(dropDown); };

                Attach(item.DropDownItems, menu, owner);
            }
        }

        private static void PlaceSubMenuLeft(ToolStripDropDown dropDown, ToolStripMenuItem item, Control owner)
        {
            try
            {
                ToolStrip ownerStrip = item.Owner;

                if (ownerStrip != null)
                {
                    Point parent = ownerStrip.PointToScreen(item.Bounds.Location);
                    dropDown.Location = new Point(parent.X - dropDown.Width - 2, parent.Y);
                }
            }
            catch
            {
            }

            ClampDropDown(dropDown, owner);
        }

        private static void ClampDropDown(ToolStripDropDown dropDown, Control owner)
        {
            Rectangle bounds = owner.RectangleToScreen(owner.ClientRectangle);
            Point location = dropDown.Location;
            Size size = dropDown.Size;

            int maxX = Math.Max(bounds.Left, bounds.Right - size.Width);
            int maxY = Math.Max(bounds.Top, bounds.Bottom - size.Height);

            int x = Math.Max(bounds.Left, Math.Min(location.X, maxX));
            int y = Math.Max(bounds.Top, Math.Min(location.Y, maxY));

            if (x != location.X || y != location.Y)
            {
                dropDown.Location = new Point(x, y);
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
        private static readonly string CacheDirectory =
            Path.Combine(AppPaths.DataDirectory, "favicons");

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
                if (DataUris.TryGetValue(key, out data))
                {
                    return data;
                }
            }

            Image image;
            string diskDataUri;

            if (TryLoadFromDisk(key, out image, out diskDataUri))
            {
                lock (Gate)
                {
                    if (!DataUris.ContainsKey(key))
                    {
                        DataUris[key] = diskDataUri;
                        Version++;
                    }

                    if (!Cache.ContainsKey(key))
                    {
                        Cache[key] = image;
                    }
                    else
                    {
                        image.Dispose();
                    }

                    return DataUris[key];
                }
            }

            return null;
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

                List<Action<Image>> pending;
                if (Pending.TryGetValue(key, out pending))
                {
                    if (onLoaded != null)
                    {
                        pending.Add(onLoaded);
                    }

                    return null;
                }
            }

            // 先读磁盘缓存；命中就不用再联网。
            Image diskImage;
            string diskDataUri;

            if (TryLoadFromDisk(key, out diskImage, out diskDataUri))
            {
                lock (Gate)
                {
                    Image cached;
                    if (Cache.TryGetValue(key, out cached))
                    {
                        diskImage.Dispose();
                        return cached;
                    }

                    Cache[key] = diskImage;

                    if (diskDataUri != null)
                    {
                        DataUris[key] = diskDataUri;
                        Version++;
                    }

                    return diskImage;
                }
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
            byte[] png = null;

            if (image != null)
            {
                try
                {
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        image.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
                        png = buffer.ToArray();
                        dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
                    }
                }
                catch
                {
                }
            }

            // 永久缓存到 data/favicons，下次启动直接读盘，不再联网。
            SaveToDisk(key, png);

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

        private static bool TryLoadFromDisk(string key, out Image image, out string dataUri)
        {
            image = null;
            dataUri = null;

            try
            {
                string path = FileFor(key);

                if (!File.Exists(path))
                {
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(path);

                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    using (Image decoded = Image.FromStream(stream))
                    {
                        image = Resize(decoded, 16, 16);
                    }
                }

                dataUri = "data:image/png;base64," + Convert.ToBase64String(bytes);
                return true;
            }
            catch
            {
                if (image != null)
                {
                    image.Dispose();
                    image = null;
                }

                dataUri = null;
                return false;
            }
        }

        private static void SaveToDisk(string key, byte[] png)
        {
            if (png == null || png.Length == 0)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(CacheDirectory);
                string path = FileFor(key);
                string temp = path + ".tmp";
                File.WriteAllBytes(temp, png);
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch
            {
            }
        }

        private static string FileFor(string key)
        {
            return Path.Combine(CacheDirectory, HashKey(key) + ".png");
        }

        private static string HashKey(string value)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder builder = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
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
