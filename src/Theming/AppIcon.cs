using System;
using System.Drawing;
using System.Windows.Forms;

namespace VantageLite.Theming
{
    /// <summary>
    /// 程序自身图标（Vantage）。依赖：System.Drawing / WinForms。
    /// 改动注意：每次返回新 Bitmap，调用方负责释放。
    /// </summary>
    internal static class AppIcon
    {
        public static Image Create16()
        {
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (icon == null)
                    {
                        return null;
                    }

                    using (Bitmap source = icon.ToBitmap())
                    {
                        Bitmap bitmap = new Bitmap(16, 16);

                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(source, 0, 0, 16, 16);
                        }

                        return bitmap;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
