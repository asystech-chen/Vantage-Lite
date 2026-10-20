using System;
using System.Collections.Generic;
using System.Text;
using VantageLite.App;
using VantageLite.Storage;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// 内置页面（历史/书签管理）的 HTML 与脚本生成。依赖：Storage / ThemeManager。
    /// 改动注意：用户数据必须 HTML 转义；书签操作通过 web message 发回宿主。
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
                    body.Append("<li><div class=\"row\">").Append(Icon(item.Url, false, i < 40))
                        .Append("<a href=\"").Append(Escape(item.Url)).Append("\">")
                        .Append(Escape(DisplayTitle(item.Title, item.Url))).Append("</a></div>")
                        .Append("<div class=\"url\">").Append(Escape(item.Url))
                        .Append(" · ").Append(FormatTime(item.VisitedUtc)).Append("</div></li>");
                }

                body.Append("</ul>");
            }

            return Page("历史", "共 " + (items == null ? 0 : items.Count) + " 条", body.ToString());
        }

        public static string Preferences(AppSettings settings)
        {
            if (settings == null)
            {
                settings = AppSettings.CreateDefault();
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"form\">");

            AppendField(body, "home", "启动页", settings.HomeUrl);
            body.Append("<label class=\"field\"><span>最大存活标签页</span>")
                .Append("<input id=\"maxTabs\" type=\"number\" min=\"1\" max=\"50\" value=\"")
                .Append(settings.MaxLiveTabs).Append("\"></label>");
            body.Append("<label class=\"check\"><input id=\"bar\" type=\"checkbox\"")
                .Append(settings.ShowBookmarksBar ? " checked" : string.Empty)
                .Append("> 显示书签栏</label>");
            body.Append("<div class=\"actions\"><button onclick=\"saveSettings()\">保存</button>")
                .Append("<span id=\"status\" class=\"status\"></span></div>");

            body.Append("</div>");

            body.Append("<script>");
            body.Append("function saveSettings(){"
                + "var home=document.getElementById('home').value.trim();"
                + "var maxTabs=parseInt(document.getElementById('maxTabs').value,10);"
                + "if(!(maxTabs>=1&&maxTabs<=50)){document.getElementById('status').textContent='标签页数量需在 1-50 之间';return;}"
                + "var bar=document.getElementById('bar').checked;"
                + "try{window.chrome.webview.postMessage(JSON.stringify({cmd:'settings',home:home,maxTabs:maxTabs,bookmarksBar:bar}));}catch(e){}"
                + "}");
            body.Append("if(window.chrome&&window.chrome.webview){"
                + "window.chrome.webview.addEventListener('message',function(e){"
                + "var s=document.getElementById('status');if(s){s.textContent=e.data||'';}});}");
            body.Append("</script>");

            return Page("设置", "Vantage Lite 基础选项", body.ToString());
        }

        private static void AppendField(StringBuilder sb, string id, string label, string value)
        {
            sb.Append("<label class=\"field\"><span>").Append(Escape(label))
                .Append("</span><input id=\"").Append(id)
                .Append("\" type=\"text\" value=\"").Append(Escape(value)).Append("\"></label>");
        }

        public static string About(string version, string runtime)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"about\">");
            AppendLine(body, "版本", string.IsNullOrEmpty(version) ? "unknown" : version);
            AppendLine(body, "WebView2 Runtime", string.IsNullOrEmpty(runtime) ? "未检测到" : runtime);
            AppendLine(body, "数据目录", AppPaths.DataDirectory);
            AppendLine(body, "定位", "轻量、可控的 Windows 浏览器");
            body.Append("</div>");

            return Page("关于 Vantage Lite", "Vantage Lite", body.ToString());
        }

        private static void AppendLine(StringBuilder sb, string name, string value)
        {
            sb.Append("<div class=\"line\"><span class=\"k\">").Append(Escape(name))
                .Append("</span><span class=\"v\">").Append(Escape(value)).Append("</span></div>");
        }

        public static string Bookmarks(BookmarkStore store, ICollection<string> expanded)
        {
            BookmarkNode root = store == null ? null : store.Root;
            int total = CountBookmarks(root, 0);
            StringBuilder body = new StringBuilder();

            body.Append("<div class=\"bar\">");
            body.Append("<button onclick=\"newFolder('").Append(BookmarkStore.ToolbarId).Append("')\">＋ 文件夹</button>");
            body.Append("<button onclick=\"toggleBar()\">书签栏</button>");
            body.Append("<span class=\"clip\">").Append(Escape(ClipboardText(store))).Append("</span>");
            body.Append("<span id=\"status\" class=\"status\">连接中…</span>");
            body.Append("</div>");

            if (total == 0)
            {
                body.Append("<p class=\"empty\">还没有书签。按 Ctrl+D 添加，或继续用下面的文件夹整理。</p>");
            }

            // 即使没有书签，也要渲染文件夹树，否则空文件夹无法管理。
            body.Append("<ul class=\"tree\">");
            AppendChildren(root, body, new int[1], expanded);
            body.Append("</ul>");

            body.Append("<div id=\"menu\" class=\"menu\">");
            body.Append("<div data-folder=\"1\" data-action=\"newFolder\" onclick=\"menuAction('newFolder')\">新建子文件夹</div>");
            body.Append("<div data-folder=\"1\" data-action=\"paste\" onclick=\"menuAction('paste')\">粘贴</div>");
            body.Append("<div data-action=\"cut\" onclick=\"menuAction('cut')\">剪切</div>");
            body.Append("<div data-action=\"copy\" onclick=\"menuAction('copy')\">复制</div>");
            body.Append("<div data-action=\"move\" onclick=\"menuAction('move')\">移动到文件夹…</div>");
            body.Append("<div data-action=\"rename\" onclick=\"menuAction('rename')\">重命名…</div>");
            body.Append("<div class=\"danger\" data-action=\"delete\" onclick=\"menuAction('delete')\">删除</div>");
            body.Append("</div>");

            body.Append("<div id=\"moveBox\" class=\"modal\"><div class=\"panel\">");
            body.Append("<div class=\"panel-title\">移动到文件夹</div>");
            body.Append("<select id=\"moveSelect\">");
            AppendFolderOptions(root, body, 0);
            body.Append("</select>");
            body.Append("<div class=\"panel-buttons\"><button onclick=\"confirmMove()\">确定</button>");
            body.Append("<button onclick=\"hideMove()\">取消</button></div></div></div>");

            body.Append("<div id=\"askBox\" class=\"modal\"><div class=\"panel\">");
            body.Append("<div class=\"panel-title\" id=\"askTitle\"></div>");
            body.Append("<input id=\"askInput\" type=\"text\">");
            body.Append("<div class=\"panel-buttons\"><button onclick=\"askOk()\">确定</button>");
            body.Append("<button onclick=\"askCancel()\">取消</button></div></div></div>");

            body.Append("<div id=\"confirmBox\" class=\"modal\"><div class=\"panel\">");
            body.Append("<div class=\"panel-title\" id=\"confirmText\"></div>");
            body.Append("<div class=\"panel-buttons\"><button class=\"danger\" onclick=\"confirmOk()\">删除</button>");
            body.Append("<button onclick=\"confirmCancel()\">取消</button></div></div></div>");

            body.Append(BuildScript(store));

            return Page("书签", "共 " + total + " 个书签 · 点行尾 ⋯ 管理", body.ToString());
        }

        private static void AppendChildren(BookmarkNode node, StringBuilder sb, int[] rendered, ICollection<string> expanded)
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
                    bool open = expanded != null && expanded.Contains(child.Id);

                    sb.Append("<li class=\"folder\" id=\"li_").Append(child.Id)
                        .Append("\" data-id=\"").Append(child.Id).Append("\">")
                        .Append("<div class=\"folder-head\" id=\"row_").Append(child.Id)
                        .Append("\" draggable=\"true\" ").Append(DragAttributes(child.Id))
                        .Append(" onclick=\"toggleFolder('").Append(child.Id)
                        .Append("',event)\"><span class=\"tri\" id=\"tri_")
                        .Append(child.Id).Append("\">").Append(open ? "▾" : "▸").Append("</span>")
                        .Append(Icon(null, true, false))
                        .Append("<span class=\"name\">")
                        .Append(Escape(child.Title)).Append("</span>")
                        .Append(MoreButton(child.Id))
                        .Append("</div><ul class=\"tree\" id=\"ul_").Append(child.Id)
                        .Append("\" style=\"display:").Append(open ? "block" : "none").Append("\">");

                    AppendChildren(child, sb, rendered, expanded);
                    sb.Append("</ul></li>");
                }
                else
                {
                    if (rendered[0] >= MaxRows)
                    {
                        continue;
                    }

                    rendered[0]++;
                    sb.Append("<li id=\"li_").Append(child.Id).Append("\" data-id=\"")
                        .Append(child.Id).Append("\"><div class=\"row\" id=\"row_").Append(child.Id)
                        .Append("\" draggable=\"true\" ").Append(DragAttributes(child.Id)).Append(">")
                        .Append(Icon(child.Url, false, rendered[0] < 40))
                        .Append("<a href=\"").Append(Escape(child.Url))
                        .Append("\" target=\"_blank\" draggable=\"false\">")
                        .Append(Escape(DisplayTitle(child.Title, child.Url))).Append("</a>")
                        .Append(MoreButton(child.Id)).Append("</div>")
                        .Append("<div class=\"url\">").Append(Escape(child.Url))
                        .Append(" · ").Append(FormatTime(child.CreatedUtc)).Append("</div></li>");
                }
            }
        }

        private static string DragAttributes(string id)
        {
            return "ondragstart=\"onDragStart(event,'" + id + "')\" "
                + "ondragend=\"onDragEnd()\" "
                + "ondragover=\"onDragOver(event,'" + id + "')\" "
                + "ondrop=\"onDrop(event,'" + id + "')\"";
        }

        private static string MoreButton(string id)
        {
            return "<button class=\"more\" onclick=\"showMenu(event,'" + id + "')\">⋯</button>";
        }

        private static string BuildScript(BookmarkStore store)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<script>");
            sb.Append("var nodes=").Append(NodesJson(store == null ? null : store.Root)).Append(";");
            sb.Append("var hasClip=").Append(store != null && store.ClipboardId != null ? "true" : "false").Append(";");
            sb.Append("var moving=null;var menuFor=null;");
            sb.Append("var FAV_GLOBE='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(\"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='6' fill='none' stroke='#999999'/><path d='M2 8h12M8 2c2.5 2.5 2.5 9.5 0 12M8 2c-2.5 2.5-2.5 9.5 0 12' fill='none' stroke='#999999'/></svg>\");");
            sb.Append("try{window.chrome.webview.postMessage(JSON.stringify({cmd:'ping'}));}catch(e){}");
            sb.Append(@"
function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}
var askCb=null,confirmCb=null;
function ask(title,value,cb){askCb=cb;document.getElementById('askTitle').textContent=title;var i=document.getElementById('askInput');i.value=value||'';document.getElementById('askBox').style.display='flex';setTimeout(function(){i.focus();i.select();},0);}
function askOk(){var i=document.getElementById('askInput');var v=i.value.trim();if(!v){return;}var cb=askCb;askCb=null;document.getElementById('askBox').style.display='none';if(cb){cb(v);}}
function askCancel(){askCb=null;document.getElementById('askBox').style.display='none';}
function newFolder(parent){ask('新建文件夹','新建文件夹',function(n){post({cmd:'newFolder',parent:parent,name:n});});}
function rename(id){ask('重命名',(nodes[id]||{}).title||'',function(n){post({cmd:'rename',id:id,name:n});});}
function del(id){confirmCb=function(){post({cmd:'delete',id:id});};document.getElementById('confirmText').textContent='删除「'+((nodes[id]||{}).title||'')+'」？';document.getElementById('confirmBox').style.display='flex';}
function confirmOk(){var cb=confirmCb;confirmCb=null;document.getElementById('confirmBox').style.display='none';if(cb){cb();}}
function confirmCancel(){confirmCb=null;document.getElementById('confirmBox').style.display='none';}
if(!window.chrome||!window.chrome.webview){
  document.body.insertAdjacentHTML('afterbegin','<div class=""warn"">页面无法与浏览器通信，书签操作不会生效。</div>');
}else{
  window.chrome.webview.addEventListener('message',function(e){
    var s=document.getElementById('status');
    if(s){s.textContent=e.data||'已连接';}
  });
}
function cut(id){post({cmd:'cut',id:id});}
function copy(id){post({cmd:'copy',id:id});}
function pasteTo(id){if(hasClip){post({cmd:'paste',parent:id});}}
function toggleBar(){post({cmd:'shortcut',name:'bookmarksbar'});}
function showMenu(ev,id){
  ev.preventDefault();ev.stopPropagation();
  menuFor=id;
  var isFolder=!!(nodes[id]&&nodes[id].folder);
  var menu=document.getElementById('menu');
  var items=menu.children;
  for(var i=0;i<items.length;i++){
    var el=items[i];
    var folderOnly=el.getAttribute('data-folder')==='1';
    var action=el.getAttribute('data-action');
    var sys=!!(nodes[id]&&nodes[id].system);
    var hide=(folderOnly&&!isFolder)||(action==='paste'&&!hasClip)||
      (sys&&(action==='delete'||action==='rename'||action==='cut'||action==='move'));
    el.style.display=hide?'none':'block';
  }
  menu.style.display='block';
  menu.style.left=Math.min(ev.pageX,window.innerWidth-200)+'px';
  menu.style.top=Math.min(ev.pageY,window.innerHeight-240)+'px';
}
function hideMenu(){var m=document.getElementById('menu');if(m){m.style.display='none';}}
document.addEventListener('click',hideMenu);
function menuAction(a){
  var id=menuFor;hideMenu();if(!id){return;}
  if(a==='newFolder'){newFolder(id);}else if(a==='paste'){pasteTo(id);}
  else if(a==='cut'){cut(id);}else if(a==='copy'){copy(id);}
  else if(a==='move'){move(id);}else if(a==='rename'){rename(id);}
  else if(a==='delete'){del(id);}
}
function toggleFolder(id,ev){
  if(ev){ev.stopPropagation();}
  var ul=document.getElementById('ul_'+id);
  var tri=document.getElementById('tri_'+id);
  if(!ul){return;}
  var open=ul.style.display!=='none';
  ul.style.display=open?'none':'block';
  if(tri){tri.textContent=open?'▸':'▾';}
  post({cmd:'expanded',id:id,open:!open});
}
function move(id){moving=id;document.getElementById('moveBox').style.display='flex';}
function hideMove(){document.getElementById('moveBox').style.display='none';}
function confirmMove(){if(moving){post({cmd:'move',id:moving,parent:document.getElementById('moveSelect').value});}hideMove();}
");
            sb.Append(@"
var draggedId=null,dropTarget=null,dropMode=null,dropEl=null;
function onDragStart(ev,id){
  draggedId=id;
  try{ev.dataTransfer.effectAllowed='move';ev.dataTransfer.setData('text/plain',id);}catch(e){}
}
function onDragOver(ev,id){
  if(!draggedId||draggedId===id){return;}
  ev.preventDefault();ev.stopPropagation();
  try{ev.dataTransfer.dropEffect='move';}catch(e){}
  var el=document.getElementById('row_'+id);if(!el){return;}
  var r=el.getBoundingClientRect();var y=(ev.clientY-r.top)/r.height;
  var f=!!(nodes[id]&&nodes[id].folder);
  var mode=(f&&y>0.3&&y<0.7)?'into':(y<0.5?'before':'after');
  setDrop(el,mode);dropTarget=id;dropMode=mode;
}
function onDrop(ev,id){
  ev.preventDefault();ev.stopPropagation();
  var mode=dropMode,target=dropTarget,dragged=draggedId;clearDrop();
  if(!dragged||!target){return;}
  if(mode==='into'){post({cmd:'move',id:dragged,parent:target});return;}
  var parent=(nodes[target]||{}).parent;
  if(!parent||parent==='root'){return;}
  var before=null;
  if(mode==='before'){before=target;}
  else{
    var li=document.getElementById('li_'+target);
    var next=li?li.nextElementSibling:null;
    while(next&&!next.getAttribute('data-id')){next=next.nextElementSibling;}
    before=next?next.getAttribute('data-id'):null;
  }
  post({cmd:'move',id:dragged,parent:parent,before:before});
}
function onDragEnd(){clearDrop();draggedId=null;}
function setDrop(el,mode){
  clearDrop();dropEl=el;el.classList.add('drop-'+mode);
}
function clearDrop(){
  if(dropEl){dropEl.classList.remove('drop-into','drop-before','drop-after');dropEl=null;}
  dropTarget=null;dropMode=null;
}
");
            sb.Append("</script>");
            return sb.ToString();
        }

        private static void AppendFolderOptions(BookmarkNode node, StringBuilder sb, int depth)
        {
            if (node == null || node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                BookmarkNode child = node.Children[i];

                if (!child.IsFolder)
                {
                    continue;
                }

                sb.Append("<option value=\"").Append(Escape(child.Id)).Append("\">");
                for (int d = 0; d < depth; d++)
                {
                    sb.Append("&#160;&#160;");
                }

                sb.Append(Escape(child.Title)).Append("</option>");
                AppendFolderOptions(child, sb, depth + 1);
            }
        }

        private static string FoldersJson(BookmarkStore store)
        {
            StringBuilder sb = new StringBuilder("[");
            if (store != null)
            {
                AppendFoldersJson(store.Root, 0, sb, new bool[1]);
            }

            return sb.Append(']').ToString();
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

        private static string NodesJson(BookmarkNode node)
        {
            StringBuilder sb = new StringBuilder("{");
            bool[] first = new bool[1] { true };
            AppendNodesJson(node, "root", sb, first);
            return sb.Append('}').ToString();
        }

        private static void AppendNodesJson(BookmarkNode node, string parentId, StringBuilder sb, bool[] first)
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
                bool system = child.Id == BookmarkStore.ToolbarId ||
                              child.Id == BookmarkStore.OtherId ||
                              child.Id == BookmarkStore.RootId;

                sb.Append(Json(child.Id)).Append(":{title:").Append(Json(child.Title))
                    .Append(",folder:").Append(child.IsFolder ? "true" : "false")
                    .Append(",system:").Append(system ? "true" : "false")
                    .Append(",parent:").Append(Json(parentId)).Append('}');

                if (child.IsFolder)
                {
                    AppendNodesJson(child, child.Id, sb, first);
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

        private static string Icon(string url, bool folder, bool warm)
        {
            if (folder)
            {
                // 与书签栏一致，优先用 Windows 自带文件夹图标。
                string shell = ShellIcons.FolderDataUri();
                if (!string.IsNullOrEmpty(shell))
                {
                    return "<img class=\"fav\" src=\"" + shell + "\">";
                }

                return "<svg class=\"fav\" viewBox=\"0 0 16 16\">"
                    + "<path d=\"M1.5 3.5h4.3l1.4 1.7h7.3a1 1 0 0 1 1 1v6.3a1 1 0 0 1-1 1h-13"
                    + "a1 1 0 0 1-1-1V4.5a1 1 0 0 1 1-1z\" fill=\"#e3b23c\"/>"
                    + "<path d=\"M1.5 5.6h13v6.9a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1z\" fill=\"#f2c94c\"/>"
                    + "</svg>";
            }

            // 优先使用已缓存的 favicon（支持 <link rel=icon> 的非根路径图标）。
            string data = FaviconCache.GetDataUri(url);
            if (!string.IsNullOrEmpty(data))
            {
                return "<img class=\"fav\" src=\"" + data + "\">";
            }

            if (warm)
            {
                FaviconCache.GetOrLoad(url, null);
            }

            string origin = Origin(url);
            if (origin == null)
            {
                return GlobeSvg();
            }

            return "<img class=\"fav\" src=\"" + Escape(origin)
                + "/favicon.ico\" onerror=\"this.onerror=null;this.src=FAV_GLOBE\">";
        }

        private static string GlobeSvg()
        {
            return "<svg class=\"fav\" viewBox=\"0 0 16 16\">"
                + "<circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"none\" stroke=\"#999999\"/>"
                + "<path d=\"M2 8h12M8 2c2.5 2.5 2.5 9.5 0 12M8 2c-2.5 2.5-2.5 9.5 0 12\" "
                + "fill=\"none\" stroke=\"#999999\"/></svg>";
        }

        private static string Origin(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Authority);
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
            ThemePalette palette = ThemeManager.Current;

            return "<!doctype html><html><head><meta charset=\"utf-8\"><base target=\"_blank\">"
                + "<title>" + Escape(title) + "</title><style>"
                + ":root{color-scheme:" + (dark ? "dark" : "light")
                + ";--bg:" + Hex(palette.WebViewBackground)
                + ";--fg:" + Hex(palette.TextPrimary)
                + ";--muted:" + Hex(palette.TextSecondary)
                + ";--border:" + Hex(palette.AddressBorder)
                + ";--link:" + Hex(palette.Accent)
                + ";--btn:" + Hex(palette.ButtonHover) + "}"
                + "body{margin:0;padding:28px 36px;background:var(--bg);color:var(--fg);"
                + "font:14px/1.6 \"Segoe UI\",\"Microsoft YaHei\",sans-serif}"
                + "h1{font-size:18px;font-weight:600;margin:0 0 4px}"
                + ".sub{color:var(--muted);font-size:12px;margin-bottom:20px}"
                + "ul{list-style:none;padding:0;margin:0}"
                + "li{padding:6px 0;border-bottom:1px solid var(--border)}"
                + "a{color:var(--link);text-decoration:none}"
                + "a:hover{text-decoration:underline}"
                + ".url{color:var(--muted);font-size:12px;word-break:break-all}"
                + ".empty{color:var(--muted);padding:24px 0}"
                + ".tree .tree{padding-left:20px}"
                + ".row{display:flex;align-items:center;gap:8px}"
                + ".name{font-weight:600}"
                + "button{font:12px/1 \"Segoe UI\",\"Microsoft YaHei\",sans-serif;padding:4px 8px;"
                + "border:1px solid var(--border);border-radius:4px;background:var(--btn);color:var(--fg);cursor:pointer}"
                + "button:hover{border-color:var(--link)}"
                + ".more{margin-left:auto;border:none;background:transparent;color:var(--muted);font-size:16px;padding:0 8px}"
                + ".more:hover{background:var(--btn);border-radius:4px}"
                + ".bar{display:flex;align-items:center;gap:8px;margin-bottom:12px}"
                + ".clip{color:var(--muted);font-size:12px;margin-left:4px}"
                + ".status{color:var(--muted);font-size:12px;margin-left:auto}"
                + ".folder-head{display:flex;align-items:center;gap:6px;cursor:pointer}"
                + ".fav{width:16px;height:16px;flex:0 0 16px;display:block;border-radius:2px}"
                + ".tri{width:14px;color:var(--muted);font-size:10px}"
                + ".menu{position:absolute;display:none;background:var(--bg);border:1px solid var(--border)"
                + ";border-radius:6px;box-shadow:0 8px 24px rgba(0,0,0,.35);padding:4px;z-index:10;min-width:160px}"
                + ".menu div{padding:6px 10px;border-radius:4px;cursor:pointer;font-size:13px}"
                + ".menu div:hover{background:var(--btn)}"
                + ".menu .danger{color:#d9534f}"
                + ".field{display:flex;flex-direction:column;gap:4px;margin-bottom:16px}"
                + ".field span{color:var(--muted);font-size:12px}"
                + ".check{display:flex;align-items:center;gap:8px;margin:8px 0 18px}"
                + ".actions{display:flex;align-items:center;gap:12px}"
                + "input[type=number],input[type=text]{padding:6px;font:14px \"Segoe UI\",\"Microsoft YaHei\",sans-serif;"
                + "box-sizing:border-box;background:var(--btn);color:var(--fg);border:1px solid var(--border);border-radius:4px}"
                + ".about .line{display:flex;gap:16px;padding:10px 0;border-bottom:1px solid var(--border)}"
                + ".about .k{width:150px;color:var(--muted)}"
                + ".about .v{word-break:break-all}"
                + ".drop-into{background:rgba(76,154,255,.16);border-radius:4px}"
                + ".drop-before{box-shadow:inset 0 2px 0 var(--link)}"
                + ".drop-after{box-shadow:inset 0 -2px 0 var(--link)}"
                + ".modal{position:fixed;inset:0;background:rgba(0,0,0,.35);display:none;align-items:center;justify-content:center}"
                + ".panel{background:var(--bg);padding:20px;border-radius:8px;min-width:320px;box-shadow:0 10px 30px rgba(0,0,0,.3)}"
                + ".panel-title{font-weight:600;margin-bottom:10px}"
                + "select{width:100%;padding:6px;font:14px \"Segoe UI\",\"Microsoft YaHei\",sans-serif;"
                + "background:var(--btn);color:var(--fg);border:1px solid var(--border);border-radius:4px}"
                + ".warn{background:#c0392b;color:#fff;padding:8px 12px;border-radius:4px;margin-bottom:12px}"
                + ".panel-buttons{margin-top:14px;display:flex;gap:8px;justify-content:flex-end}"
                + "</style><script>var VLT_THEME=" + ThemeJson() + ";"
                + "function vantageApplyTheme(dark){var t=VLT_THEME[dark?'dark':'light'];if(!t){return;}"
                + "var s=document.documentElement.style;s.setProperty('color-scheme',dark?'dark':'light');"
                + "s.setProperty('--bg',t.bg);s.setProperty('--fg',t.fg);s.setProperty('--muted',t.muted);"
                + "s.setProperty('--border',t.border);s.setProperty('--link',t.link);s.setProperty('--btn',t.btn);}"
                + "</script></head><body><h1>" + Escape(title) + "</h1>"
                + "<div class=\"sub\">" + Escape(subtitle) + "</div>"
                + body + "</body></html>";
        }

        /// <summary>
        /// 主题切换时注入内置页的脚本；页面里没有定义时安全跳过。
        /// </summary>
        public static string ThemeScript()
        {
            return "if(window.vantageApplyTheme){vantageApplyTheme("
                + (ThemeManager.IsDark ? "true" : "false") + ");}";
        }

        private static string ThemeJson()
        {
            return "{dark:" + ThemeObject(ThemePalette.Dark) + ",light:" + ThemeObject(ThemePalette.Light) + "}";
        }

        private static string ThemeObject(ThemePalette palette)
        {
            return "{bg:'" + Hex(palette.WebViewBackground)
                + "',fg:'" + Hex(palette.TextPrimary)
                + "',muted:'" + Hex(palette.TextSecondary)
                + "',border:'" + Hex(palette.AddressBorder)
                + "',link:'" + Hex(palette.Accent)
                + "',btn:'" + Hex(palette.ButtonHover) + "'}";
        }

        private static string Hex(System.Drawing.Color color)
        {
            return "#" + color.R.ToString("x2") + color.G.ToString("x2") + color.B.ToString("x2");
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
