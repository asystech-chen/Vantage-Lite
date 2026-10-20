using System;
using System.Diagnostics;
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

    /// <summary>
    /// 关于窗口：参考 Vantage 的关于对话框（图标 + 名称 + 版本 + 说明 + 链接）。
    /// 依赖：ThemeManager / AppIcon。改动注意：模态窗口，只读信息。
    /// </summary>
    internal static class AboutDialog
    {
        public static bool Show(IWin32Window owner, string version)
        {
            bool diagnosticsRequested = false;

            using (Form form = new Form())
            {
                ThemePalette palette = ThemeManager.Current;

                form.Text = "关于 Vantage Lite";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ClientSize = new Size(470, 340);
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = ThemeFonts.Ui(9.5F);
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                PictureBox icon = new PictureBox();
                icon.SetBounds(28, 26, 64, 64);
                icon.SizeMode = PictureBoxSizeMode.Zoom;
                icon.Image = LoadIcon(64);

                Label title = new Label();
                title.Text = "Vantage Lite";
                title.Font = ThemeFonts.Ui(15F);
                title.ForeColor = palette.TextPrimary;
                title.SetBounds(108, 28, 330, 28);

                Label versionLabel = new Label();
                versionLabel.Text = "版本 " + (string.IsNullOrWhiteSpace(version) ? "unknown" : version);
                versionLabel.ForeColor = palette.TextSecondary;
                versionLabel.SetBounds(110, 60, 320, 20);

                Label description = new Label();
                description.Text = "轻量、可控的 Windows 浏览器\n基于 WebView2，本地数据，无账号、无遥测。";
                description.ForeColor = palette.TextPrimary;
                description.SetBounds(28, 118, 414, 52);

                LinkLabel repo = new LinkLabel();
                repo.Text = "github.com/asystech-chen/Vantage-Lite";
                repo.LinkColor = palette.Accent;
                repo.ActiveLinkColor = palette.Accent;
                repo.VisitedLinkColor = palette.Accent;
                repo.SetBounds(28, 180, 414, 20);
                repo.LinkClicked += delegate
                {
                    try
                    {
                        Process.Start("https://github.com/asystech-chen/Vantage-Lite");
                    }
                    catch
                    {
                    }
                };

                Label license = new Label();
                license.Text = "Mozilla Public License 2.0";
                license.ForeColor = palette.TextSecondary;
                license.SetBounds(28, 206, 414, 20);

                Label copyright = new Label();
                copyright.Text = "© 2026 asystech-chen";
                copyright.ForeColor = palette.TextSecondary;
                copyright.SetBounds(28, 284, 200, 20);

                Button diagnostics = CreateButton("诊断信息", palette);
                diagnostics.SetBounds(250, 282, 100, 30);
                diagnostics.Click += delegate
                {
                    diagnosticsRequested = true;
                    form.Close();
                };

                Button ok = CreateButton("确定", palette);
                ok.SetBounds(358, 282, 84, 30);
                ok.Click += delegate { form.Close(); };

                form.Controls.Add(icon);
                form.Controls.Add(title);
                form.Controls.Add(versionLabel);
                form.Controls.Add(description);
                form.Controls.Add(repo);
                form.Controls.Add(license);
                form.Controls.Add(copyright);
                form.Controls.Add(diagnostics);
                form.Controls.Add(ok);

                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    ok.Focus();
                };

                form.FormClosed += delegate
                {
                    Image image = icon.Image;
                    icon.Image = null;

                    if (image != null)
                    {
                        image.Dispose();
                    }
                };

                form.ShowDialog(owner);
                return diagnosticsRequested;
            }
        }

        private static Image LoadIcon(int size)
        {
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (icon == null)
                    {
                        return null;
                    }

                    Bitmap bitmap = new Bitmap(size, size);

                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawIcon(icon, new Rectangle(0, 0, size, size));
                    }

                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Button CreateButton(string text, ThemePalette palette)
        {
            Button button = new Button();
            button.Text = text;
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

    /// <summary>
    /// 诊断信息弹出窗口：只读文本 + 复制/打开目录。
    /// 依赖：ThemeManager。改动注意：窗口是模态的，调用方在 UI 线程。
    /// </summary>
    internal static class DiagnosticsForm
    {
        public static void Show(IWin32Window owner, string text, string dataDirectory, string logDirectory)
        {
            using (Form form = new Form())
            {
                ThemePalette palette = ThemeManager.Current;

                form.Text = "Vantage Lite 诊断信息";
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(740, 580);
                form.MinimumSize = new Size(560, 420);
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = ThemeFonts.Ui(9.5F);
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                Panel bottom = new Panel();
                bottom.Dock = DockStyle.Bottom;
                bottom.Height = 46;
                bottom.BackColor = palette.WindowBackground;

                TextBox box = new TextBox();
                box.Multiline = true;
                box.ReadOnly = true;
                box.ScrollBars = ScrollBars.Both;
                box.WordWrap = false;
                box.Dock = DockStyle.Fill;
                box.BorderStyle = BorderStyle.FixedSingle;
                box.BackColor = palette.ChromeBackground;
                box.ForeColor = palette.TextPrimary;
                box.Font = new Font("Consolas", 9F);
                box.Text = text ?? string.Empty;

                Button copy = CreateButton("复制全部", palette);
                copy.Click += delegate
                {
                    try
                    {
                        Clipboard.SetText(box.Text);
                    }
                    catch
                    {
                    }
                };

                Button openData = CreateButton("打开数据目录", palette);
                openData.Click += delegate { OpenDirectory(dataDirectory); };

                Button openLogs = CreateButton("打开日志目录", palette);
                openLogs.Click += delegate { OpenDirectory(logDirectory); };

                Button close = CreateButton("关闭", palette);
                close.Click += delegate { form.Close(); };

                int right = bottom.Width;
                bottom.Controls.Add(close);
                bottom.Controls.Add(openLogs);
                bottom.Controls.Add(openData);
                bottom.Controls.Add(copy);

                close.SetBounds(0, 8, 84, 30);
                openLogs.SetBounds(0, 8, 110, 30);
                openData.SetBounds(0, 8, 110, 30);
                copy.SetBounds(0, 8, 84, 30);

                bottom.Layout += delegate
                {
                    int x = bottom.Width - 12 - 84;
                    close.Left = x;
                    x -= 110 + 8;
                    openLogs.Left = x;
                    x -= 110 + 8;
                    openData.Left = x;
                    x -= 84 + 8;
                    copy.Left = x;
                };

                form.Controls.Add(box);
                form.Controls.Add(bottom);
                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    box.SelectionStart = 0;
                    box.SelectionLength = 0;
                };

                form.ShowDialog(owner);
            }
        }

        private static void OpenDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch
            {
            }
        }

        private static Button CreateButton(string text, ThemePalette palette)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 30;
            button.Width = 84;
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
}
