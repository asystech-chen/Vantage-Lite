using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 启动阶段的提示框：WebView2 缺失引导与便携版说明。依赖：ThemeManager / ThemeFonts / FlatButton。
    /// 改动注意：在 MainForm 创建前调用；尺寸按系统 DPI 与「文本大小」缩放。
    /// </summary>
    internal static class StartupDialogs
    {
        /// <summary>WebView2 不可用提示；runtimeRegistered 表示有残留注册信息。返回 true 表示用户选择下载。</summary>
        public static bool ShowWebViewMissing(bool runtimeRegistered)
        {
            string text = runtimeRegistered
                ? "检测到 WebView2 的安装记录，但运行时无法使用。常见原因是卸载不完整或注册信息损坏，安装程序会误报「已安装」。\n\n" +
                  "请先在「设置 → 应用」中卸载 Microsoft Edge WebView2 Runtime；如果找不到这一项，请修复或重装 Microsoft Edge。然后重新安装 WebView2，安装完成后重新启动 Vantage Lite。"
                : "Vantage Lite 使用 Microsoft Edge WebView2 内核，当前系统没有检测到它。\n\n" +
                  "点击「下载安装包」会在默认浏览器中打开 Microsoft 官方 Evergreen 独立安装包（x64）。安装完成后，请重新启动 Vantage Lite。";

            return ShowNotice(
                "需要 WebView2 运行时",
                text,
                "下载安装包",
                "退出");
        }

        /// <summary>便携版说明：返回 true 继续启动，false 表示用户要先搬目录。</summary>
        public static bool ShowPortableNotice()
        {
            return ShowNotice(
                "便携版使用提示",
                "Vantage Lite 是便携版。浏览器配置、书签、历史记录和日志都保存在程序所在目录（UserData、data、Logs）。\n\n" +
                "建议把程序放入一个空目录后再使用，避免这些自动生成的文件与当前目录的其他文件混在一起。移动时请整个目录一起移动，数据会一并带走。",
                "别啰嗦，立刻启动",
                "关掉程序，我要搬一下");
        }

        private static bool ShowNotice(string title, string text, string primaryText, string secondaryText)
        {
            int dpi = GetSystemDpi();

            Func<int, int> px = delegate(int value)
            {
                return (int)Math.Round(value * dpi / 96.0);
            };

            double textScale = ReadTextScaleFactor();

            using (Font font = ThemeFonts.Ui((float)(9.5 * textScale)))
            using (Form form = new Form())
            {
                ThemePalette palette = ThemeManager.Current;

                form.Text = title;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = true;
                form.Font = font;
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                int margin = px(20);
                int gap = px(8);
                Button primary = CreateButton(primaryText, palette, font, px);
                primary.DialogResult = DialogResult.OK;
                form.AcceptButton = primary;

                Button secondary = null;
                if (!string.IsNullOrEmpty(secondaryText))
                {
                    secondary = CreateButton(secondaryText, palette, font, px);
                    secondary.DialogResult = DialogResult.Cancel;
                    form.CancelButton = secondary;
                }

                // 按钮文字很长或系统「文本大小」放大时，按按钮总宽撑宽对话框。
                int width = Math.Max(
                    px(460),
                    margin * 2 + primary.Width + (secondary == null ? 0 : gap + secondary.Width));

                int textWidth = width - margin * 2;
                Size measured = TextRenderer.MeasureText(
                    text,
                    font,
                    new Size(textWidth, px(2000)),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

                int textHeight = Math.Max(px(24), measured.Height + px(4));
                int buttonTop = margin + textHeight + px(20);
                int buttonHeight = Math.Max(px(30), font.Height + px(10));
                form.ClientSize = new Size(width, buttonTop + buttonHeight + px(18));

                Label label = new Label();
                label.AutoSize = false;
                label.Text = text;
                label.ForeColor = palette.TextPrimary;
                label.SetBounds(margin, margin, textWidth, textHeight);
                form.Controls.Add(label);

                int right = width - margin;

                if (secondary == null)
                {
                    primary.SetBounds(right - primary.Width, buttonTop, primary.Width, buttonHeight);
                }
                else
                {
                    secondary.SetBounds(right - secondary.Width, buttonTop, secondary.Width, buttonHeight);
                    primary.SetBounds(
                        secondary.Left - gap - primary.Width,
                        buttonTop,
                        primary.Width,
                        buttonHeight);

                    form.Controls.Add(secondary);
                }

                form.Controls.Add(primary);

                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    primary.Focus();
                };

                return form.ShowDialog() == DialogResult.OK;
            }
        }

        private static Button CreateButton(string text, ThemePalette palette, Font font, Func<int, int> px)
        {
            Button button = new FlatButton();
            button.Text = text;
            button.Height = px(30);
            button.Width = Math.Max(px(84), TextRenderer.MeasureText(text, font).Width + px(24));
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.ChromeBackground;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = palette.AddressBorder;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonPressed;
            return button;
        }

        /// <summary>无窗口上下文时取系统 DPI（主显示器）。</summary>
        private static int GetSystemDpi()
        {
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    int dpi = (int)Math.Round(graphics.DpiX);
                    return dpi > 0 ? dpi : 96;
                }
            }
            catch
            {
                return 96;
            }
        }

        /// <summary>
        /// 读取 Windows「文本大小」缩放因子（100–225），失败或未设置时返回 1.0。
        /// </summary>
        private static double ReadTextScaleFactor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Accessibility"))
                {
                    object value = key == null ? null : key.GetValue("TextScaleFactor");

                    if (value != null)
                    {
                        int factor = Convert.ToInt32(value);

                        if (factor >= 100 && factor <= 225)
                        {
                            return factor / 100.0;
                        }
                    }
                }
            }
            catch
            {
            }

            return 1.0;
        }
    }
}
