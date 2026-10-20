using System.Drawing;
using System.Drawing.Text;

namespace VantageLite.Theming
{
    /// <summary>
    /// 图标字体：Win11 用 Segoe Fluent Icons，Win10 回退 Segoe MDL2 Assets。
    /// 依赖：System.Drawing。改动注意：新增图标时优先用两套字体都有的码位。
    /// </summary>
    public static class IconFont
    {
        public const string Back = "\uE72B";
        public const string Forward = "\uE72A";
        public const string Refresh = "\uE72C";
        public const string Cancel = "\uE711";
        public const string Home = "\uE80F";
        public const string Add = "\uE710";
        public const string Close = "\uE8BB";
        public const string Star = "\uE734";
        public const string StarFilled = "\uE735";
        public const string More = "\uE712";
        public const string Folder = "\uE8B7";
        public const string Menu = "\uE700";
        public const string Download = "\uE896";
        public const string OpenFile = "\uE8E5";
        public const string OpenFolder = "\uE838";
        public const string Delete = "\uE74D";
        public const string Pause = "\uE769";
        public const string Play = "\uE768";
        public const string Retry = "\uE72C";
        public const string Lock = "\uE72E";
        public const string Unlock = "\uE785";

        private static readonly string Family = DetectFamily();

        public static Font Create(float size)
        {
            return new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        /// <summary>
        /// 按像素创建图标字体：不随系统 DPI 放大，避免小按钮里的图标过大被裁成省略号。
        /// </summary>
        public static Font CreatePx(float pixels)
        {
            return new Font(Family, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static string DetectFamily()
        {
            try
            {
                using (InstalledFontCollection fonts = new InstalledFontCollection())
                {
                    for (int i = 0; i < fonts.Families.Length; i++)
                    {
                        if (fonts.Families[i].Name == "Segoe Fluent Icons")
                        {
                            return "Segoe Fluent Icons";
                        }
                    }

                    for (int i = 0; i < fonts.Families.Length; i++)
                    {
                        if (fonts.Families[i].Name == "Segoe MDL2 Assets")
                        {
                            return "Segoe MDL2 Assets";
                        }
                    }
                }
            }
            catch
            {
            }

            return "Segoe UI Symbol";
        }
    }
}
