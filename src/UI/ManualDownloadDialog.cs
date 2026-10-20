using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 手动下载地址框：默认浏览器和 IE 都打不开时使用。
    /// 依赖：ThemeManager / ThemeFonts / FlatButton。改动注意：地址必须是只读但可选中、可复制的文本框。
    /// </summary>
    internal static class ManualDownloadDialog
    {
        public static void Show(string url)
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

                form.Text = "手动下载 WebView2";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = true;
                form.Font = font;
                form.BackColor = palette.WindowBackground;
                form.ForeColor = palette.TextPrimary;

                int width = px(640);
                int margin = px(20);
                int textWidth = width - margin * 2;

                string prompt = "无法打开默认浏览器和 Internet Explorer。请复制下面的地址，在任意浏览器中打开并下载安装：";

                Size measured = TextRenderer.MeasureText(
                    prompt,
                    font,
                    new Size(textWidth, px(2000)),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

                int textHeight = Math.Max(px(24), measured.Height + px(4));
                int boxTop = margin + textHeight + px(14);

                Label label = new Label();
                label.AutoSize = false;
                label.Text = prompt;
                label.ForeColor = palette.TextPrimary;
                label.SetBounds(margin, margin, textWidth, textHeight);
                form.Controls.Add(label);

                TextBox address = new TextBox();
                address.Text = url;
                address.ReadOnly = true;
                address.BorderStyle = BorderStyle.FixedSingle;
                address.BackColor = palette.ChromeBackground;
                address.ForeColor = palette.TextPrimary;
                address.Font = font;
                int addressHeight = Math.Max(px(28), address.PreferredHeight);
                address.SetBounds(margin, boxTop, textWidth, addressHeight);
                form.Controls.Add(address);

                int buttonTop = boxTop + addressHeight + px(18);
                int buttonHeight = Math.Max(px(30), font.Height + px(10));
                int gap = px(8);

                Button copy = CreateButton("复制链接", palette, font, px);
                copy.Click += delegate
                {
                    try
                    {
                        Clipboard.SetText(url);
                        copy.Text = "已复制";
                    }
                    catch
                    {
                    }
                };

                Button close = CreateButton("关闭", palette, font, px);
                close.DialogResult = DialogResult.Cancel;

                int right = width - margin;
                close.SetBounds(right - close.Width, buttonTop, close.Width, buttonHeight);
                copy.SetBounds(
                    close.Left - gap - copy.Width,
                    buttonTop,
                    copy.Width,
                    buttonHeight);

                form.Controls.Add(copy);
                form.Controls.Add(close);
                form.CancelButton = close;
                form.AcceptButton = copy;
                form.ClientSize = new Size(width, buttonTop + buttonHeight + px(18));

                form.Shown += delegate
                {
                    ThemeManager.ApplyWindowTheme(form);
                    address.Focus();
                    address.SelectAll();
                };

                form.ShowDialog();
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

        /// <summary>读取 Windows「文本大小」缩放因子（100–225）。</summary>
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
