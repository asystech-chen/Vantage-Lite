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
    /// 依赖：BookmarkStore 的节点 / ThemeManager。改动注意：只渲染工具栏文件夹，整棵树不进内存 UI。
    /// </summary>
    public sealed class BookmarksBar : UserControl
    {
        public event EventHandler<string> OpenRequested;
        public event EventHandler ManageRequested;

        private readonly FlowLayoutPanel _panel;
        private readonly Button _manageButton;
        private readonly ToolTip _toolTip;
        private List<Button> _buttons;

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
            _panel.MouseUp += Panel_MouseUp;

            _manageButton = new Button();
            _manageButton.Text = IconFont.More;
            _manageButton.Width = 30;
            _manageButton.Height = 24;
            _manageButton.FlatStyle = FlatStyle.Flat;
            _manageButton.FlatAppearance.BorderSize = 0;
            _manageButton.Font = IconFont.Create(10F);
            _manageButton.TabStop = false;
            _manageButton.Cursor = Cursors.Default;
            _manageButton.Dock = DockStyle.Right;
            _manageButton.Click += delegate { Raise(ManageRequested); };
            _toolTip.SetToolTip(_manageButton, "书签管理器 (Ctrl+Alt+B)");

            Controls.Add(_panel);
            Controls.Add(_manageButton);

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

        public void Refresh(BookmarkNode toolbar)
        {
            ClearButtons();

            if (toolbar == null)
            {
                return;
            }

            IList<BookmarkNode> children = toolbar.GetChildren();
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
            _manageButton.BackColor = palette.ChromeBackground;
            _manageButton.ForeColor = palette.TextSecondary;
            _manageButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            _manageButton.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;

            for (int i = 0; i < _buttons.Count; i++)
            {
                ApplyButtonTheme(_buttons[i]);
            }
        }

        private Button CreateItemButton(BookmarkNode node)
        {
            string text = node.IsFolder ? node.Title + " ▾" : node.Title;
            Button button = new Button();
            button.Text = text;
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(4, 0, 4, 0);
            button.Height = 24;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TabStop = false;
            button.Cursor = Cursors.Default;
            button.Font = ThemeFonts.Ui(9.5F);

            int width = TextRenderer.MeasureText(text, button.Font).Width + 14;
            button.Width = Math.Max(36, Math.Min(180, width));

            if (node.IsFolder)
            {
                button.Click += delegate { ShowFolderMenu(button, node); };
                _toolTip.SetToolTip(button, node.Title);
            }
            else
            {
                string url = node.Url;
                button.Click += delegate { RaiseOpen(url); };
                _toolTip.SetToolTip(button, url);
            }

            ApplyButtonTheme(button);
            return button;
        }

        private void ShowFolderMenu(Button button, BookmarkNode folder)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = ThemeFonts.Ui(9.5F);

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

            // 立即 Dispose 会让 ToolStripManager 在菜单关闭流程里访问已释放对象。
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

            Point location = button.PointToScreen(new Point(0, button.Height));
            menu.Show(location);
        }

        private ToolStripMenuItem CreateMenuItem(BookmarkNode node)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(node.Title);
            item.ToolTipText = node.IsFolder ? null : node.Url;

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

        private void Panel_MouseUp(object sender, MouseEventArgs e)
        {
            // 空白处右键打开管理器。
            if (e.Button == MouseButtons.Right)
            {
                Raise(ManageRequested);
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
