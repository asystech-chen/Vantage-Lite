using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 浏览器书签导入选择框：勾选检测到的浏览器配置，或改走文件导入。
    /// 依赖：BookmarkSources / ThemeManager。改动注意：只读检测结果，不做文件 IO。
    /// </summary>
    internal sealed class ImportBookmarksForm : Form
    {
        private readonly CheckedListBox _list;
        private readonly Button _importButton;

        public ImportBookmarksForm(IList<BookmarkSources.Source> sources, string note)
        {
            SelectedSources = new List<BookmarkSources.Source>();

            Text = "导入书签";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(520, 406);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = ThemeFonts.Ui(9.5F);

            ThemePalette palette = ThemeManager.Current;
            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;

            bool hasSources = sources != null && sources.Count > 0;

            Label caption = new Label();
            caption.Text = hasSources
                ? "选择要导入的浏览器配置（可多选）："
                : "未检测到已安装浏览器的书签。";
            caption.SetBounds(16, 14, 488, 20);
            caption.ForeColor = palette.TextSecondary;

            _list = new CheckedListBox();
            _list.SetBounds(16, 40, 488, 238);
            _list.CheckOnClick = true;
            _list.IntegralHeight = false;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.BackColor = palette.AddressBackground;
            _list.ForeColor = palette.TextPrimary;
            _list.Enabled = hasSources;

            if (hasSources)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    _list.Items.Add(sources[i], true);
                }
            }

            _list.ItemCheck += delegate
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                try
                {
                    BeginInvoke(new Action(UpdateImportState));
                }
                catch (InvalidOperationException)
                {
                }
            };

            Label noteLabel = new Label();
            noteLabel.SetBounds(16, 286, 488, 52);
            noteLabel.ForeColor = palette.TextSecondary;
            noteLabel.Text = string.IsNullOrWhiteSpace(note)
                ? (hasSources
                    ? "支持 Chrome、Edge、Brave、Vivaldi、Opera、Firefox；也可以从 HTML 或 .url 文件导入。"
                    : "可以使用「从文件…」导入 HTML 或 .url 文件。")
                : note;

            _importButton = CreateButton("导入选中", palette);
            _importButton.SetBounds(196, 348, 104, 30);
            _importButton.Enabled = hasSources;
            _importButton.Click += delegate
            {
                SelectedSources = CollectSelected();

                if (SelectedSources.Count == 0)
                {
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            Button fileButton = CreateButton("从文件…", palette);
            fileButton.SetBounds(308, 348, 96, 30);
            fileButton.Click += delegate
            {
                UseFiles = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button cancelButton = CreateButton("取消", palette);
            cancelButton.SetBounds(412, 348, 92, 30);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(caption);
            Controls.Add(_list);
            Controls.Add(noteLabel);
            Controls.Add(_importButton);
            Controls.Add(fileButton);
            Controls.Add(cancelButton);

            AcceptButton = _importButton;
            CancelButton = cancelButton;

            Shown += delegate { ThemeManager.ApplyWindowTheme(this); };
        }

        public List<BookmarkSources.Source> SelectedSources { get; private set; }

        public bool UseFiles { get; private set; }

        private List<BookmarkSources.Source> CollectSelected()
        {
            List<BookmarkSources.Source> selected = new List<BookmarkSources.Source>();

            foreach (object item in _list.CheckedItems)
            {
                BookmarkSources.Source source = item as BookmarkSources.Source;

                if (source != null)
                {
                    selected.Add(source);
                }
            }

            return selected;
        }

        private void UpdateImportState()
        {
            if (_importButton != null)
            {
                _importButton.Enabled = _list.CheckedItems.Count > 0;
            }
        }

        private static Button CreateButton(string text, ThemePalette palette)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.BackColor = palette.ChromeBackground;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            return button;
        }
    }
}
