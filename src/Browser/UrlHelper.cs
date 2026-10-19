using System;

namespace VantageLite.Browser
{
    /// <summary>
    /// 把地址栏输入规范化成可导航的 URL，非 URL 输入转搜索。
    /// 依赖：无。改动注意：搜索词必须完整转义，避免特殊字符破坏 URL。
    /// </summary>
    public static class UrlHelper
    {
        private const string SearchUrl = "https://www.bing.com/search?q=";

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            input = input.Trim();

            if (input.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return input;
            }

            Uri uri;
            if (Uri.TryCreate(input, UriKind.Absolute, out uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    return input;
                }
            }

            if (input.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("[::1]", StringComparison.OrdinalIgnoreCase))
            {
                return "http://" + input;
            }

            bool looksLikeHost = input.IndexOf(' ') < 0 &&
                                 (input.IndexOf('.') >= 0 || input.IndexOf(':') >= 0);

            if (looksLikeHost)
            {
                return "https://" + input;
            }

            return SearchUrl + Uri.EscapeDataString(input);
        }
    }
}
