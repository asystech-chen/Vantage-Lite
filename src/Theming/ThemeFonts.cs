using System.Drawing;
using System.Windows.Forms;

namespace VantageLite.Theming
{
    /// <summary>
    /// 统一使用 Windows 的系统 UI 字体，避免硬编码字体名。
    /// 依赖：SystemFonts。改动注意：不同 Windows 版本会返回不同字体（如 Segoe UI Variable）。
    /// </summary>
    public static class ThemeFonts
    {
        public static Font Ui()
        {
            return SystemFonts.MessageBoxFont;
        }

        public static Font Ui(float size)
        {
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size);
        }

        public static Font Ui(float size, FontStyle style)
        {
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
        }
    }
}
