using System;
using System.Collections.Generic;
using System.Drawing;
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
        private BookmarkStore _store;

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
            MouseUp += Bar_MouseUp;

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
                ClearButtons();
            }

            base.Dispose(disposing);
        }

        public void Refresh(BookmarkStore store)
        {
            _store = store;
            ClearButtons();

            if (_store == null || _store.ToolbarFolder == null)
            {
                return;
            }

            IList<BookmarkNode> children = _store.ToolbarFolder.GetChildren();
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

            // AutoEllipsis 会额外弹出“完整文本”系统提示，和自定义 ToolTip 重叠；
            // 这里手动截断，保证只有一个提示来源。
            text = Shorten(text, button.Font, 166);
            button.Text = text;

            int width = TextRenderer.MeasureText(text, button.Font).Width + 14;
            button.Width = Math.Max(36, Math.Min(180, width));

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
                if (e.Button == MouseButtons.Right)
                {
                    ShowItemMenu(button, node);
                }
            };

            ApplyButtonTheme(button);
            return button;
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
                ToolStripMenuItem item = MenuItem(label, delegate { Move(node.Id, id); });
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

        private void Move(string id, string parentId)
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
}
