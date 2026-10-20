using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 书签管理器：以目录树展示书签，支持新建文件夹、重命名、删除、打开。
    /// 依赖：BookmarkStore / ThemeManager。改动注意：所有修改立即通过 Store 落盘。
    /// </summary>
    public sealed class BookmarkManagerForm : Form
    {
        private readonly BookmarkStore _store;
        private TreeView _tree;

        public BookmarkManagerForm(BookmarkStore store)
        {
            _store = store;

            Text = "书签管理器";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 520);
            MinimumSize = new Size(420, 360);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = ThemeFonts.Ui(9.5F);
            BackColor = ThemeManager.Current.ChromeBackground;
            ForeColor = ThemeManager.Current.TextPrimary;

            BuildControls();
            ReloadTree(null);
        }

        public event EventHandler<string> OpenRequested;

        private void BuildControls()
        {
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 46;
            bottom.BackColor = BackColor;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(8, 8, 8, 8);

            Button close = CreateButton("关闭");
            Button delete = CreateButton("删除");
            Button rename = CreateButton("重命名");
            Button addFolder = CreateButton("新建文件夹");
            Button open = CreateButton("打开");

            close.Click += delegate { Close(); };
            delete.Click += delegate { DeleteSelected(); };
            rename.Click += delegate { RenameSelected(); };
            addFolder.Click += delegate { AddFolder(); };
            open.Click += delegate { OpenSelected(); };

            buttons.Controls.Add(close);
            buttons.Controls.Add(delete);
            buttons.Controls.Add(rename);
            buttons.Controls.Add(addFolder);
            buttons.Controls.Add(open);
            bottom.Controls.Add(buttons);

            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.FullRowSelect = true;
            _tree.ShowLines = false;
            _tree.ShowRootLines = false;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = ThemeManager.Current.AddressBackground;
            _tree.ForeColor = ThemeManager.Current.TextPrimary;
            _tree.DoubleClick += delegate { OpenSelected(); };

            Controls.Add(_tree);
            Controls.Add(bottom);
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 92;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = ThemeManager.Current.ButtonHover;
            button.ForeColor = ThemeManager.Current.TextPrimary;
            button.Margin = new Padding(4, 0, 0, 0);
            button.Cursor = Cursors.Default;
            return button;
        }

        private void ReloadTree(string selectId)
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            foreach (BookmarkNode child in _store.Root.GetChildren())
            {
                _tree.Nodes.Add(CreateTreeNode(child));
            }

            _tree.ExpandAll();
            _tree.EndUpdate();

            if (!string.IsNullOrEmpty(selectId))
            {
                SelectNode(_tree.Nodes, selectId);
            }
            else if (_tree.Nodes.Count > 0)
            {
                _tree.SelectedNode = _tree.Nodes[0];
            }
        }

        private static TreeNode CreateTreeNode(BookmarkNode node)
        {
            string prefix = node.IsFolder ? "📁 " : "🔖 ";
            TreeNode treeNode = new TreeNode(prefix + node.Title);
            treeNode.Tag = node;

            if (node.IsFolder)
            {
                foreach (BookmarkNode child in node.GetChildren())
                {
                    treeNode.Nodes.Add(CreateTreeNode(child));
                }
            }

            return treeNode;
        }

        private static bool SelectNode(TreeNodeCollection nodes, string id)
        {
            foreach (TreeNode node in nodes)
            {
                BookmarkNode data = node.Tag as BookmarkNode;
                if (data != null && string.Equals(data.Id, id, StringComparison.Ordinal))
                {
                    node.TreeView.SelectedNode = node;
                    return true;
                }

                if (SelectNode(node.Nodes, id))
                {
                    return true;
                }
            }

            return false;
        }

        private BookmarkNode SelectedNode()
        {
            TreeNode node = _tree == null ? null : _tree.SelectedNode;
            return node == null ? null : node.Tag as BookmarkNode;
        }

        private void AddFolder()
        {
            BookmarkNode selected = SelectedNode();
            string parentId = ResolveParentId(selected);

            string name = Prompt("新建文件夹", "文件夹名称：", "新建文件夹");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            BookmarkNode created = _store.AddFolder(parentId, name);
            ReloadTree(created == null ? null : created.Id);
        }

        private void RenameSelected()
        {
            BookmarkNode selected = SelectedNode();
            if (selected == null)
            {
                return;
            }

            if (selected.Id == BookmarkStore.ToolbarId ||
                selected.Id == BookmarkStore.OtherId ||
                selected.Id == BookmarkStore.RootId)
            {
                MessageBox.Show(this, "系统文件夹不能重命名。", "书签管理器",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = Prompt("重命名", "新名称：", selected.Title);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _store.Rename(selected.Id, name);
            ReloadTree(selected.Id);
        }

        private void DeleteSelected()
        {
            BookmarkNode selected = SelectedNode();
            if (selected == null)
            {
                return;
            }

            if (selected.Id == BookmarkStore.ToolbarId ||
                selected.Id == BookmarkStore.OtherId ||
                selected.Id == BookmarkStore.RootId)
            {
                MessageBox.Show(this, "系统文件夹不能删除。", "书签管理器",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = selected.IsFolder
                ? "删除文件夹「" + selected.Title + "」及其中的全部内容？"
                : "删除书签「" + selected.Title + "」？";

            DialogResult result = MessageBox.Show(this, message, "书签管理器",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _store.Remove(selected.Id);
                ReloadTree(null);
            }
        }

        private void OpenSelected()
        {
            BookmarkNode selected = SelectedNode();
            if (selected == null || selected.IsFolder)
            {
                return;
            }

            EventHandler<string> handler = OpenRequested;
            if (handler != null)
            {
                handler(this, selected.Url);
            }

            Close();
        }

        private string ResolveParentId(BookmarkNode selected)
        {
            if (selected == null)
            {
                return BookmarkStore.ToolbarId;
            }

            if (selected.IsFolder)
            {
                return selected.Id;
            }

            BookmarkNode parent = _store.ParentOf(selected.Id);
            return parent == null ? BookmarkStore.ToolbarId : parent.Id;
        }

        private string Prompt(string title, string label, string value)
        {
            using (PromptForm form = new PromptForm(title, label, value))
            {
                return form.ShowDialog(this) == DialogResult.OK ? form.Value : null;
            }
        }

        private sealed class PromptForm : Form
        {
            private readonly TextBox _box;

            public PromptForm(string title, string label, string value)
            {
                Text = title;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                ClientSize = new Size(380, 132);
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;
                Font = ThemeFonts.Ui(9.5F);

                Label caption = new Label();
                caption.Text = label;
                caption.Left = 16;
                caption.Top = 16;
                caption.Width = 340;

                _box = new TextBox();
                _box.Left = 16;
                _box.Top = 42;
                _box.Width = 348;
                _box.Text = value ?? string.Empty;

                Button ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.Left = 208;
                ok.Top = 82;
                ok.Width = 74;

                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Left = 290;
                cancel.Top = 82;
                cancel.Width = 74;

                Controls.Add(caption);
                Controls.Add(_box);
                Controls.Add(ok);
                Controls.Add(cancel);

                AcceptButton = ok;
                CancelButton = cancel;
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                _box.Focus();
                _box.SelectAll();
            }

            public string Value
            {
                get { return _box.Text.Trim(); }
            }
        }
    }
}
