using System.Drawing;

namespace VantageLite.Theming
{
    /// <summary>
    /// 明暗两套配色。所有界面颜色都从这里取，禁止在控件里硬编码。
    /// 依赖：无。改动注意：新增颜色时两套都要补，保持键一致。
    /// </summary>
    public sealed class ThemePalette
    {
        public Color WindowBackground;
        public Color ChromeBackground;
        public Color TabStripBackground;
        public Color TabActive;
        public Color TabInactive;
        public Color TabHover;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextDisabled;
        public Color AddressBackground;
        public Color AddressBorder;
        public Color ButtonHover;
        public Color ButtonPressed;
        public Color CaptionHover;
        public Color CaptionPressed;
        public Color Accent;
        public Color Warning;
        public Color CloseHover;
        public Color WebViewBackground;

        public static readonly ThemePalette Dark = new ThemePalette
        {
            WindowBackground = Color.FromArgb(24, 27, 33),
            ChromeBackground = Color.FromArgb(30, 34, 42),
            TabStripBackground = Color.FromArgb(22, 25, 31),
            TabActive = Color.FromArgb(42, 47, 58),
            TabInactive = Color.FromArgb(28, 32, 39),
            TabHover = Color.FromArgb(36, 41, 51),
            TextPrimary = Color.FromArgb(242, 245, 250),
            TextSecondary = Color.FromArgb(168, 176, 191),
            TextDisabled = Color.FromArgb(150, 158, 172),
            AddressBackground = Color.FromArgb(40, 45, 56),
            AddressBorder = Color.FromArgb(58, 65, 80),
            ButtonHover = Color.FromArgb(47, 54, 67),
            ButtonPressed = Color.FromArgb(58, 67, 82),
            CaptionHover = Color.FromArgb(40, 45, 54),
            CaptionPressed = Color.FromArgb(52, 58, 69),
            Accent = Color.FromArgb(76, 154, 255),
            Warning = Color.FromArgb(240, 173, 78),
            CloseHover = Color.FromArgb(196, 43, 28),
            WebViewBackground = Color.FromArgb(24, 27, 33)
        };

        public static readonly ThemePalette Light = new ThemePalette
        {
            WindowBackground = Color.FromArgb(243, 245, 249),
            ChromeBackground = Color.FromArgb(255, 255, 255),
            TabStripBackground = Color.FromArgb(232, 236, 242),
            TabActive = Color.FromArgb(255, 255, 255),
            TabInactive = Color.FromArgb(221, 227, 235),
            TabHover = Color.FromArgb(230, 235, 242),
            TextPrimary = Color.FromArgb(27, 31, 39),
            TextSecondary = Color.FromArgb(90, 98, 112),
            TextDisabled = Color.FromArgb(150, 158, 170),
            AddressBackground = Color.FromArgb(241, 244, 248),
            AddressBorder = Color.FromArgb(201, 209, 220),
            ButtonHover = Color.FromArgb(232, 237, 244),
            ButtonPressed = Color.FromArgb(220, 227, 236),
            CaptionHover = Color.FromArgb(214, 221, 231),
            CaptionPressed = Color.FromArgb(199, 208, 220),
            Accent = Color.FromArgb(15, 108, 189),
            Warning = Color.FromArgb(163, 80, 0),
            CloseHover = Color.FromArgb(196, 43, 28),
            WebViewBackground = Color.FromArgb(255, 255, 255)
        };
    }
}
