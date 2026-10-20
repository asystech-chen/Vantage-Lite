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

                ThemePalette palette = ThemeManager.Current;
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                Label caption = new Label();
                caption.Text = label;
                caption.Left = 16;
                caption.Top = 16;
                caption.Width = 340;
                caption.ForeColor = palette.TextSecondary;

                TextBox box = new TextBox();
                box.Left = 16;
                box.Top = 42;
                box.Width = 348;
                box.Text = value ?? string.Empty;
                box.BorderStyle = BorderStyle.FixedSingle;
                box.BackColor = palette.AddressBackground;
                box.ForeColor = palette.TextPrimary;

                Button ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.Left = 208;
                ok.Top = 82;
                ok.Width = 74;
                ApplyButtonTheme(ok, palette);

                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Left = 290;
                cancel.Top = 82;
                cancel.Width = 74;
                ApplyButtonTheme(cancel, palette);

                form.Controls.Add(caption);
                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;

                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    box.Focus();
                    box.SelectAll();
                };

                return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }

        private static void ApplyButtonTheme(Button button, ThemePalette palette)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.ChromeBackground;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.BorderColor = palette.AddressBorder;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
        }
    }
}

    /// <summary>
    /// 主题化消息框，替代系统 MessageBox，避免深色模式下弹出白底窗口。
    /// 依赖：ThemeManager。改动注意：只覆盖提示与确认两类按钮组合，不处理输入。
    /// </summary>
    internal static class MessageDialog
    {
        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons)
        {
            using (Form form = new Form())
            {
                ThemePalette palette = ThemeManager.Current;

                form.Text = string.IsNullOrEmpty(caption) ? "Vantage Lite" : caption;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = ThemeFonts.Ui(9.5F);
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                Size measured = TextRenderer.MeasureText(
                    text ?? string.Empty,
                    form.Font,
                    new Size(364, 1000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

                int textHeight = Math.Max(24, Math.Min(400, measured.Height + 4));
                int buttonTop = 18 + textHeight + 18;
                form.ClientSize = new Size(400, buttonTop + 30 + 18);

                Label label = new Label();
                label.Text = text ?? string.Empty;
                label.SetBounds(18, 18, 364, textHeight);
                label.ForeColor = palette.TextPrimary;
                form.Controls.Add(label);

                Button ok = CreateButton("确定", DialogResult.OK, palette);
                Button cancel = CreateButton("取消", DialogResult.Cancel, palette);
                Button yes = CreateButton("是", DialogResult.Yes, palette);
                Button no = CreateButton("否", DialogResult.No, palette);

                Button primary = ok;
                Button secondary = null;

                if (buttons == MessageBoxButtons.YesNo)
                {
                    primary = yes;
                    secondary = no;
                }
                else if (buttons == MessageBoxButtons.OKCancel)
                {
                    primary = ok;
                    secondary = cancel;
                }

                const int width = 84;
                const int gap = 8;
                int right = 400 - 18;

                if (secondary != null)
                {
                    secondary.SetBounds(right - width, buttonTop, width, 28);
                    form.Controls.Add(secondary);

                    primary.SetBounds(right - width - gap - width, buttonTop, width, 28);
                    form.CancelButton = secondary;
                }
                else
                {
                    primary.SetBounds(right - width, buttonTop, width, 28);
                }

                form.Controls.Add(primary);
                form.AcceptButton = primary;

                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    primary.Focus();
                };

                return form.ShowDialog(owner);
            }
        }

        private static Button CreateButton(string text, DialogResult result, ThemePalette palette)
        {
            Button button = new Button();
            button.Text = text;
            button.DialogResult = result;
            button.Width = 84;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.ChromeBackground;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.BorderColor = palette.AddressBorder;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            return button;
        }
    }

