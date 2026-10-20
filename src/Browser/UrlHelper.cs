using System;
using System.Collections.Generic;
using VantageLite.Storage;

namespace VantageLite.Browser
{
    /// <summary>
    /// 把地址栏输入规范化成可导航的 URL，非 URL 输入转搜索。
    /// 依赖：无。改动注意：搜索词必须完整转义，避免特殊字符破坏 URL。
    /// </summary>
    public static class UrlHelper
    {
        private const string DefaultSearchUrl = "https://www.bing.com/search?q=";

        public static string Normalize(string input)
        {
            return Normalize(input, null);
        }

        public static string Normalize(string input, string searchEngine)
        {
            return Normalize(input, searchEngine, null);
        }

        public static string Normalize(string input, string searchEngine, IList<CustomSearchEngine> customEngines)
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

            return BuildSearchUrl(input, searchEngine, customEngines);
        }

        /// <summary>
        /// 地址栏输入是否是没有写协议的域名（可自动补全 https/www/http）。
        /// 含空格、显式协议、内部页都不算。
        /// </summary>
        public static bool IsAutoSchemeCandidate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string value = input.Trim();

            if (value.IndexOf(' ') >= 0)
            {
                return false;
            }

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("vantage:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return value.IndexOf('.') >= 0 || value.IndexOf(':') >= 0;
        }

        /// <summary>
        /// 自动补全候选：https://host → https://www.host → http://host → http://www.host。
        /// 只在原地址是自动补出的 https 时使用；IP、localhost 和不带 www 的显式地址不展开。
        /// </summary>
        public static string[] BuildFallbackChain(string url)
        {
            Uri uri;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (uri.HostNameType == UriHostNameType.IPv4 ||
                uri.HostNameType == UriHostNameType.IPv6 ||
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { uri.AbsoluteUri };
            }

            bool hasWww = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
            string primary = uri.AbsoluteUri;
            List<string> candidates = new List<string>();
            candidates.Add(primary);

            if (!hasWww)
            {
                candidates.Add(WithHost(primary, "www." + uri.Host, null));
            }

            candidates.Add(WithHost(primary, null, Uri.UriSchemeHttp));

            if (!hasWww)
            {
                candidates.Add(WithHost(primary, "www." + uri.Host, Uri.UriSchemeHttp));
            }

            return candidates.ToArray();
        }

        private static string WithHost(string url, string host, string scheme)
        {
            UriBuilder builder = new UriBuilder(url);

            if (!string.IsNullOrEmpty(host))
            {
                builder.Host = host;
            }

            if (!string.IsNullOrEmpty(scheme))
            {
                builder.Scheme = scheme;
            }

            return builder.Uri.AbsoluteUri;
        }

        public static string BuildSearchUrl(string query, string searchEngine, IList<CustomSearchEngine> customEngines)
        {
            string escaped = Uri.EscapeDataString(query ?? string.Empty);
            string template = SearchTemplate(searchEngine, customEngines);

            if (string.IsNullOrEmpty(template))
            {
                return SearchUrlFor(null) + escaped;
            }

            int index = template.IndexOf("{query}", StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                return template.Substring(0, index) + escaped + template.Substring(index + 7);
            }

            return template + escaped;
        }

        /// <summary>
        /// 返回带 {query} 占位符的搜索模板；自定义引擎按 custom:名称 查找。
        /// </summary>
        public static string SearchTemplate(string searchEngine, IList<CustomSearchEngine> customEngines)
        {
            string key = searchEngine ?? string.Empty;

            if (key.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                string name = key.Substring(7).Trim();

                if (customEngines != null)
                {
                    for (int i = 0; i < customEngines.Count; i++)
                    {
                        CustomSearchEngine engine = customEngines[i];

                        if (engine != null &&
                            string.Equals(engine.Name, name, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(engine.Url))
                        {
                            return engine.Url;
                        }
                    }
                }

                return null;
            }

            switch (key.ToLowerInvariant())
            {
                case "baidu":
                    return "https://www.baidu.com/s?wd={query}";
                case "google":
                    return "https://www.google.com/search?q={query}";
                case "duckduckgo":
                    return "https://duckduckgo.com/?q={query}";
                default:
                    return "https://www.bing.com/search?q={query}";
            }
        }

        public static string EngineKey(CustomSearchEngine engine)
        {
            return engine == null ? string.Empty : "custom:" + (engine.Name ?? string.Empty).Trim();
        }

        public static string SearchUrlPrefix(string searchEngine)
        {
            return SearchUrlFor(searchEngine);
        }

        private static string SearchUrlFor(string searchEngine)
        {
            switch ((searchEngine ?? string.Empty).ToLowerInvariant())
            {
                case "google":
                    return "https://www.google.com/search?q=";
                case "baidu":
                    return "https://www.baidu.com/s?wd=";
                case "duckduckgo":
                    return "https://duckduckgo.com/?q=";
                default:
                    return DefaultSearchUrl;
            }
        }
    }
}
