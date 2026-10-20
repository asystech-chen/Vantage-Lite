using System;
using System.Collections.Generic;
using System.Text;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 内置页面（历史/书签）的 HTML 生成。依赖：Storage / ThemeManager。
    /// 改动注意：所有用户数据必须 HTML 转义后再输出。
    /// </summary>
    internal static class InternalPages
    {
        private const int MaxRows = 500;

        public static string History(IList<HistoryEntry> items)
        {
            StringBuilder body = new StringBuilder();

            if (items == null || items.Count == 0)
            {
                body.Append("<p class=\"empty\">还没有浏览记录。</p>");
            }
            else
            {
                body.Append("<ul>");
                int count = Math.Min(items.Count, MaxRows);

                for (int i = 0; i < count; i++)
                {
                    HistoryEntry item = items[i];
                    body.Append("<li><a href=\"").Append(Escape(item.Url)).Append("\">")
                        .Append(Escape(DisplayTitle(item.Title, item.Url))).Append("</a>")
                        .Append("<div class=\"url\">").Append(Escape(item.Url))
                        .Append(" · ").Append(FormatTime(item.VisitedUtc)).Append("</div></li>");
                }

                body.Append("</ul>");
            }

            return Page("历史", "共 " + (items == null ? 0 : items.Count) + " 条", body.ToString());
        }

        public static string Bookmarks(IList<Bookmark> items)
        {
            StringBuilder body = new StringBuilder();

            if (items == null || items.Count == 0)
            {
                body.Append("<p class=\"empty\">还没有书签。打开页面后按 Ctrl+D 添加。</p>");
            }
            else
            {
                body.Append("<ul>");

                for (int i = 0; i < items.Count; i++)
                {
                    Bookmark item = items[i];
                    body.Append("<li><a href=\"").Append(Escape(item.Url)).Append("\">")
                        .Append(Escape(DisplayTitle(item.Title, item.Url))).Append("</a>")
                        .Append("<div class=\"url\">").Append(Escape(item.Url))
                        .Append(" · ").Append(FormatTime(item.CreatedUtc)).Append("</div></li>");
                }

                body.Append("</ul>");
            }

            return Page("书签", "共 " + (items == null ? 0 : items.Count) + " 条", body.ToString());
        }

        private static string Page(string title, string subtitle, string body)
        {
            bool dark = ThemeManager.IsDark;
            string background = dark ? "#1e1f22" : "#ffffff";
            string foreground = dark ? "#e4e4e4" : "#222222";
            string muted = dark ? "#9aa0a6" : "#888888";
            string border = dark ? "#33363b" : "#eeeeee";
            string link = dark ? "#6cb6ff" : "#1a66c9";

            return "<!doctype html><html><head><meta charset=\"utf-8\"><base target=\"_blank\">"
                + "<title>" + Escape(title) + "</title><style>"
                + ":root{color-scheme:" + (dark ? "dark" : "light") + "}"
                + "body{margin:0;padding:28px 36px;background:" + background + ";color:" + foreground + ";"
                + "font:14px/1.6 \"Segoe UI\",\"Microsoft YaHei\",sans-serif}"
                + "h1{font-size:18px;font-weight:600;margin:0 0 4px}"
                + ".sub{color:" + muted + ";font-size:12px;margin-bottom:20px}"
                + "ul{list-style:none;padding:0;margin:0}"
                + "li{padding:8px 0;border-bottom:1px solid " + border + "}"
                + "a{color:" + link + ";text-decoration:none}"
                + "a:hover{text-decoration:underline}"
                + ".url{color:" + muted + ";font-size:12px;word-break:break-all}"
                + ".empty{color:" + muted + ";padding:24px 0}"
                + "</style></head><body><h1>" + Escape(title) + "</h1>"
                + "<div class=\"sub\">" + Escape(subtitle) + "</div>"
                + body + "</body></html>";
        }

        private static string DisplayTitle(string title, string url)
        {
            return string.IsNullOrWhiteSpace(title) ? url : title;
        }

        private static string FormatTime(long unixSeconds)
        {
            if (unixSeconds <= 0)
            {
                return string.Empty;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
