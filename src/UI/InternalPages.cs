using System;
using System.Collections.Generic;
using System.Text;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 内置页面（历史/书签管理）的 HTML 与脚本生成。依赖：Storage / ThemeManager。
    /// 改动注意：所有用户数据必须 HTML 转义；书签操作通过 web message 发回宿主。
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

        public static string Bookmarks(BookmarkStore store)
        {
            BookmarkNode root = store == null ? null : store.Root;
            int total = CountBookmarks(root, 0);
            StringBuilder body = new StringBuilder();

            body.Append("<div class=\"bar\">");
            body.Append("<button onclick=\"newFolder('").Append(BookmarkStore.ToolbarId).Append("')\">＋ 文件夹</button>");
            body.Append("<button onclick=\"pasteTo('").Append(BookmarkStore.ToolbarId).Append("')\"")
                .Append(store != null && store.ClipboardId != null ? string.Empty : " disabled")
                .Append(">粘贴</button>");
            body.Append("<button onclick=\"toggleBar()\">书签栏</button>");
            body.Append("<span class=\"clip\">").Append(Escape(ClipboardText(store))).Append("</span>");
            body.Append("</div>");

            if (total == 0)
            {
                body.Append("<p class=\"empty\">还没有书签。打开页面后按 Ctrl+D 添加到书签工具栏。</p>");
            }
            else
            {
                body.Append("<ul class=\"tree\">");
                AppendChildren(root, body, new int[1]);
                body.Append("</ul>");
            }

            body.Append("<div id=\"moveBox\" class=\"modal\"><div class=\"panel\">");
            body.Append("<div class=\"panel-title\">移动到文件夹</div>");
            body.Append("<select id=\"moveSelect\"></select>");
            body.Append("<div class=\"panel-buttons\"><button onclick=\"confirmMove()\">确定</button>");
            body.Append("<button onclick=\"hideMove()\">取消</button></div></div></div>");

            body.Append(BuildScript(store));

            return Page("书签", "共 " + total + " 个书签", body.ToString());
        }

        private static void AppendChildren(BookmarkNode node, StringBuilder sb, int[] rendered)
        {
            if (node == null || node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (child.IsFolder)
                {
                    sb.Append("<li class=\"folder\"><div class=\"row\"><span class=\"name\">📁 ")
                        .Append(Escape(child.Title)).Append("</span><span class=\"acts\">")
                        .Append(Button("＋", "newFolder('" + child.Id + "')", false))
                        .Append(Button("粘贴", "pasteTo('" + child.Id + "')", true))
                        .Append(Button("重命名", "rename('" + child.Id + "')", false))
                        .Append(Button("剪切", "cut('" + child.Id + "')", false))
                        .Append(Button("复制", "copy('" + child.Id + "')", false))
                        .Append(Button("移动", "move('" + child.Id + "')", false))
                        .Append(Button("删除", "del('" + child.Id + "')", false))
                        .Append("</span></div><ul class=\"tree\">");

                    AppendChildren(child, sb, rendered);
                    sb.Append("</ul></li>");
                }
                else
                {
                    if (rendered[0] >= MaxRows)
                    {
                        continue;
                    }

                    rendered[0]++;
                    sb.Append("<li><div class=\"row\"><a href=\"").Append(Escape(child.Url))
                        .Append("\" target=\"_blank\">")
                        .Append(Escape(DisplayTitle(child.Title, child.Url))).Append("</a>")
                        .Append("<span class=\"acts\">")
                        .Append(Button("重命名", "rename('" + child.Id + "')", false))
                        .Append(Button("剪切", "cut('" + child.Id + "')", false))
                        .Append(Button("复制", "copy('" + child.Id + "')", false))
                        .Append(Button("移动", "move('" + child.Id + "')", false))
                        .Append(Button("删除", "del('" + child.Id + "')", false))
                        .Append("</span></div>")
                        .Append("<div class=\"url\">").Append(Escape(child.Url))
                        .Append(" · ").Append(FormatTime(child.CreatedUtc)).Append("</div></li>");
                }
            }
        }

        private static string Button(string text, string onClick, bool paste)
        {
            // 粘贴按钮在无剪贴板时由脚本统一禁用（data-paste 标记）。
            string extra = paste ? " data-paste=\"1\"" : string.Empty;
            return "<button" + extra + " onclick=\"" + onClick + "\">" + Escape(text) + "</button>";
        }

        private static string BuildScript(BookmarkStore store)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<script>");
            sb.Append("var folders=").Append(FoldersJson(store)).Append(";");
            sb.Append("var titles=").Append(TitlesJson(store == null ? null : store.Root)).Append(";");
            sb.Append("var hasClip=").Append(store != null && store.ClipboardId != null ? "true" : "false").Append(";");
            sb.Append("var moving=null;");
            sb.Append(@"
function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}
function newFolder(parent){var n=prompt('文件夹名称','新建文件夹');if(n&&n.trim()){post({cmd:'newFolder',parent:parent,name:n.trim()});}}
function rename(id){var n=prompt('新名称',titles[id]||'');if(n&&n.trim()){post({cmd:'rename',id:id,name:n.trim()});}}
function del(id){if(confirm('删除「'+(titles[id]||'')+'」？')){post({cmd:'delete',id:id});}}
function cut(id){post({cmd:'cut',id:id});}
function copy(id){post({cmd:'copy',id:id});}
function pasteTo(id){if(hasClip){post({cmd:'paste',parent:id});}}
function toggleBar(){post({cmd:'shortcut',name:'bookmarksbar'});}
function move(id){
  moving=id;
  var s=document.getElementById('moveSelect');
  s.innerHTML='';
  for(var i=0;i<folders.length;i++){
    var f=folders[i];
    var o=document.createElement('option');
    o.value=f.id;
    o.textContent=new Array(f.depth+1).join('  ')+f.title;
    s.appendChild(o);
  }
  document.getElementById('moveBox').style.display='flex';
}
function hideMove(){document.getElementById('moveBox').style.display='none';}
function confirmMove(){if(moving){post({cmd:'move',id:moving,parent:document.getElementById('moveSelect').value});}hideMove();}
if(!hasClip){var bs=document.querySelectorAll('button[data-paste]');for(var i=0;i<bs.length;i++){bs[i].disabled=true;}}
");
            sb.Append("</script>");
            return sb.ToString();
        }

        private static string FoldersJson(BookmarkStore store)
        {
            StringBuilder sb = new StringBuilder("[");
            if (store != null)
            {
                AppendFoldersJson(store.Root, 0, sb, new bool[1]);
            }

            return sb.Append("]").ToString();
        }

        private static void AppendFoldersJson(BookmarkNode node, int depth, StringBuilder sb, bool[] first)
        {
            if (node == null || node.Children == null)
            {
                return;
            }

            if (depth > 0)
            {
                if (!first[0])
                {
                    sb.Append(',');
                }

                first[0] = false;
                sb.Append("{id:").Append(Json(node.Id))
                    .Append(",title:").Append(Json(node.Title))
                    .Append(",depth:").Append(depth - 1).Append('}');
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];
                if (child.IsFolder)
                {
                    AppendFoldersJson(child, depth + 1, sb, first);
                }
            }
        }

        private static string TitlesJson(BookmarkNode node)
        {
            StringBuilder sb = new StringBuilder("{");
            bool[] first = new bool[1] { true };
            AppendTitlesJson(node, sb, first);
            return sb.Append('}').ToString();
        }

        private static void AppendTitlesJson(BookmarkNode node, StringBuilder sb, bool[] first)
        {
            if (node == null || node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (!first[0])
                {
                    sb.Append(',');
                }

                first[0] = false;
                sb.Append(Json(child.Id)).Append(':').Append(Json(child.Title));

                if (child.IsFolder)
                {
                    AppendTitlesJson(child, sb, first);
                }
            }
        }

        private static string ClipboardText(BookmarkStore store)
        {
            if (store == null || string.IsNullOrEmpty(store.ClipboardId))
            {
                return "剪贴板为空";
            }

            return (store.ClipboardCut ? "已剪切「" : "已复制「") + store.ClipboardTitle + "」";
        }

        private static int CountBookmarks(BookmarkNode node, int count)
        {
            if (node == null || node.Children == null)
            {
                return count;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (child.IsFolder)
                {
                    count = CountBookmarks(child, count);
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        private static string Page(string title, string subtitle, string body)
        {
            bool dark = ThemeManager.IsDark;
            string background = dark ? "#1e1f22" : "#ffffff";
            string foreground = dark ? "#e4e4e4" : "#222222";
            string muted = dark ? "#9aa0a6" : "#888888";
            string border = dark ? "#33363b" : "#eeeeee";
            string link = dark ? "#6cb6ff" : "#1a66c9";
            string buttonBg = dark ? "#2b2f36" : "#f1f4f8";

            return "<!doctype html><html><head><meta charset=\"utf-8\"><base target=\"_blank\">"
                + "<title>" + Escape(title) + "</title><style>"
                + ":root{color-scheme:" + (dark ? "dark" : "light") + "}"
                + "body{margin:0;padding:28px 36px;background:" + background + ";color:" + foreground + ";"
                + "font:14px/1.6 \"Segoe UI\",\"Microsoft YaHei\",sans-serif}"
                + "h1{font-size:18px;font-weight:600;margin:0 0 4px}"
                + ".sub{color:" + muted + ";font-size:12px;margin-bottom:20px}"
                + "ul{list-style:none;padding:0;margin:0}"
                + "li{padding:6px 0;border-bottom:1px solid " + border + "}"
                + "a{color:" + link + ";text-decoration:none}"
                + "a:hover{text-decoration:underline}"
                + ".url{color:" + muted + ";font-size:12px;word-break:break-all}"
                + ".empty{color:" + muted + ";padding:24px 0}"
                + ".tree .tree{padding-left:20px}"
                + ".row{display:flex;align-items:center;gap:8px}"
                + ".name{font-weight:600}"
                + ".acts{margin-left:auto;display:flex;gap:4px;flex-wrap:wrap}"
                + "button{font:12px/1 \"Segoe UI\",\"Microsoft YaHei\",sans-serif;padding:4px 8px;"
                + "border:1px solid " + border + ";border-radius:4px;background:" + buttonBg + ";color:" + foreground + ";cursor:pointer}"
                + "button:hover{border-color:" + link + "}"
                + "button:disabled{opacity:.4;cursor:default}"
                + ".bar{display:flex;align-items:center;gap:8px;margin-bottom:12px}"
                + ".clip{color:" + muted + ";font-size:12px;margin-left:4px}"
                + ".modal{position:fixed;inset:0;background:rgba(0,0,0,.35);display:none;align-items:center;justify-content:center}"
                + ".panel{background:" + background + ";padding:20px;border-radius:8px;min-width:320px;box-shadow:0 10px 30px rgba(0,0,0,.3)}"
                + ".panel-title{font-weight:600;margin-bottom:10px}"
                + "select{width:100%;padding:6px;font:14px \"Segoe UI\",\"Microsoft YaHei\",sans-serif}"
                + ".panel-buttons{margin-top:14px;display:flex;gap:8px;justify-content:flex-end}"
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

        private static string Json(string text)
        {
            if (text == null)
            {
                return "null";
            }

            return "\"" + text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
        }
    }
}
