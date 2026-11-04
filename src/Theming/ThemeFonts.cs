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

        /// <summary>
        /// 按 96 DPI 设计尺寸 × 当前缩放系数创建字体（像素单位）。
        /// 用于跟随 WebView2 RasterizationScale 缩放的主界面文本，避免点单位受系统字体缩放影响而不跟随。
        /// </summary>
        public static Font UiPx(float pointSize)
        {
            return UiPx(pointSize, FontStyle.Regular);
        }

        public static Font UiPx(float pointSize, FontStyle style)
        {
            float pixels = pointSize * 96f / 72f * (float)VantageLite.UI.WindowChrome.ScaleFactor;
            return new Font(SystemFonts.MessageBoxFont.FontFamily, pixels, style, GraphicsUnit.Pixel);
        }
    }
}
