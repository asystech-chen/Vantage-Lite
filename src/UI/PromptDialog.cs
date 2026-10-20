using System;
using System.Drawing;
using System.Windows.Forms;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 简单的单行输入对话框。依赖：ThemeManager。改动注意：只在 UI 线程调用。
    /// </summary>
    internal static class PromptDialog
    {
        public static string Ask(IWin32Window owner, string title, string label, string value)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ClientSize = new Size(380, 132);
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = ThemeFonts.Ui(9.5F);

                Label caption = new Label();
                caption.Text = label;
                caption.Left = 16;
                caption.Top = 16;
                caption.Width = 340;

                TextBox box = new TextBox();
                box.Left = 16;
                box.Top = 42;
                box.Width = 348;
                box.Text = value ?? string.Empty;

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

                form.Controls.Add(caption);
                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;

                form.Shown += delegate
                {
                    box.Focus();
                    box.SelectAll();
                };

                return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }
    }
}
