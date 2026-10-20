using System;
using System.Collections.Generic;
using System.Text;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Downloads;
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
            int total = items == null ? 0 : items.Count;

            body.Append("<div class=\"filters\">");
            AppendHistoryFilter(body, "today", "今天");
            AppendHistoryFilter(body, "d7", "最近 7 天");
            AppendHistoryFilter(body, "d30", "最近 30 天");
            AppendHistoryFilter(body, "d180", "最近半年");
            AppendHistoryFilter(body, "d365", "最近一年");
            AppendHistoryFilter(body, "all", "全部");
            body.Append("<input id=\"histQuery\" class=\"search\" type=\"text\" placeholder=\"搜索历史\" oninput=\"applyFilter()\">");
            body.Append("<span id=\"shown\" class=\"status\"></span>");
            body.Append("<button class=\"danger\" onclick=\"askClear()\">清空历史</button>");
            body.Append("</div>");

            body.Append("<ul id=\"histList\">");

            for (int i = 0; i < total; i++)
            {
                HistoryEntry item = items[i];
                body.Append("<li data-ts=\"").Append(item.VisitedUtc).Append("\">")
                    .Append("<div class=\"row\">").Append(Icon(item.Url, false, i < 40))
                    .Append("<a href=\"").Append(Escape(item.Url)).Append("\">")
                    .Append(Escape(DisplayTitle(item.Title, item.Url))).Append("</a></div>")
                    .Append("<div class=\"url\">").Append(Escape(item.Url))
                    .Append(" · ").Append(FormatTime(item.VisitedUtc)).Append("</div></li>");
            }

            body.Append("</ul>");
            body.Append("<p id=\"histEmpty\" class=\"empty\" style=\"display:none\">该时间段没有记录。</p>");

            body.Append("<div id=\"clearBox\" class=\"modal\"><div class=\"panel\">");
            body.Append("<div class=\"panel-title\">清空全部浏览历史？</div>");
            body.Append("<div class=\"panel-buttons\"><button class=\"danger\" onclick=\"clearOk()\">清空</button>");
            body.Append("<button onclick=\"hideClear()\">取消</button></div></div></div>");

            body.Append("<script>");
            body.Append("function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}");
            body.Append("var RANGE_DAYS={today:0,d7:7,d30:30,d180:180,d365:365,all:-1};");
            body.Append("var currentRange='all';");
            body.Append("function setRange(key){currentRange=key;applyFilter();}");
            body.Append("function applyFilter(){"
                + "var q=(document.getElementById('histQuery').value||'').trim().toLowerCase();"
                + "var now=Date.now();var from=0;"
                + "if(currentRange==='today'){var d=new Date();d.setHours(0,0,0,0);from=d.getTime();}"
                + "else if(currentRange!=='all'){from=now-RANGE_DAYS[currentRange]*86400000;}"
                + "var items=document.querySelectorAll('#histList li');var n=0;"
                + "for(var i=0;i<items.length;i++){"
                + "var ts=parseInt(items[i].getAttribute('data-ts'),10)*1000;"
                + "var show=(currentRange==='all'||ts>=from);"
                + "if(show&&q){show=items[i].textContent.toLowerCase().indexOf(q)>=0;}"
                + "items[i].style.display=show?'':'none';if(show){n++;}}"
                + "var bs=document.querySelectorAll('.filter');"
                + "for(var j=0;j<bs.length;j++){bs[j].className='filter'+(bs[j].getAttribute('data-range')===currentRange?' active':'');}"
                + "document.getElementById('shown').textContent='显示 '+n+' 条';"
                + "document.getElementById('histEmpty').style.display=n===0?'block':'none';}");
            body.Append("function askClear(){document.getElementById('clearBox').style.display='flex';}");
            body.Append("function hideClear(){document.getElementById('clearBox').style.display='none';}");
            body.Append("function clearOk(){hideClear();post({cmd:'clearHistory'});}");
            body.Append("setRange('all');");
            body.Append("</script>");

            return Page("历史", "共 " + total + " 条", body.ToString());
        }

        public static string Downloads(IList<DownloadItem> items)
        {
            StringBuilder body = new StringBuilder();
            int total = items == null ? 0 : items.Count;

            body.Append("<style>")
                .Append(".dl-list li{display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid var(--border)}")
                .Append(".dl-main{min-width:0;flex:1}")
                .Append(".dl-name{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}")
                .Append(".dl-actions{display:flex;gap:4px;flex:0 0 auto}")
                .Append(".icon-btn{width:30px;height:30px;padding:0;display:flex;align-items:center;justify-content:center;")
                .Append("border:none;background:transparent;color:var(--muted);border-radius:6px;cursor:pointer}")
                .Append(".icon-btn:hover{background:var(--btn);color:var(--fg)}")
                .Append(".icon-btn.danger:hover{color:#d9534f}")
                .Append(".icon-btn svg{width:16px;height:16px}")
                .Append(".dl-status{color:var(--muted);font-size:12px;margin-top:2px}")
                .Append(".dl-bar{height:4px;background:var(--btn);border-radius:2px;margin-top:6px;overflow:hidden}")
                .Append(".dl-bar i{display:block;height:100%;background:var(--link);width:0}")
                .Append("</style>");

            body.Append("<div class=\"bar\"><span class=\"status\">共 ").Append(total).Append(" 条</span></div>");
            body.Append("<ul class=\"dl-list\">");

            for (int i = 0; i < total; i++)
            {
                DownloadItem item = items[i];
                string id = Escape(item.Id);

                body.Append("<li data-id=\"").Append(id).Append("\"><div class=\"dl-main\"><div class=\"dl-name\">")
                    .Append(Escape(item.DisplayName)).Append("</div><div class=\"url\">")
                    .Append(Escape(item.Url)).Append("</div><div class=\"dl-status\">")
                    .Append(Escape(DescribeDownload(item))).Append("</div>");

                // 只有进行中/暂停的任务显示进度条；完成、取消、失败不显示。
                if (item.IsActive)
                {
                    body.Append("<div class=\"dl-bar\"><i style=\"width:")
                        .Append(DownloadPercent(item)).Append("%\"></i></div>");
                }

                body.Append("</div><div class=\"dl-actions\">");

                if (item.IsActive)
                {
                    if (item.Paused)
                    {
                        AppendDownloadIcon(body, "pause", id, "继续", IconPlay);
                    }
                    else
                    {
                        AppendDownloadIcon(body, "pause", id, "暂停", IconPause);
                    }

                    AppendDownloadIcon(body, "cancel", id, "取消下载", IconCancel);
                }
                else if (item.IsCompleted)
                {
                    AppendDownloadIcon(body, "open", id, "打开文件", IconOpen);
                    AppendDownloadIcon(body, "folder", id, "打开所在文件夹", IconFolder);
                    AppendDownloadIcon(body, "delete", id, "删除记录", IconDelete);
                }
                else
                {
                    AppendDownloadIcon(body, "retry", id, "重试", IconRetry);
                    AppendDownloadIcon(body, "delete", id, "删除记录", IconDelete);
                }

                body.Append("</div></li>");
            }

            body.Append("</ul>");

            if (total == 0)
            {
                body.Append("<p class=\"empty\">暂无下载记录。</p>");
            }

            body.Append("<script>");
            body.Append("function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}");
            body.Append("function act(a,id){post({cmd:'downloadAction',name:a,id:id});}");
            body.Append("document.addEventListener('click',function(e){"
                + "var b=e.target&&e.target.closest?e.target.closest('.icon-btn'):null;"
                + "if(b){act(b.getAttribute('data-action'),b.getAttribute('data-id'));}});");
            body.Append("function applyStatus(items){var map={};"
                + "for(var i=0;i<items.length;i++){map[items[i].id]=items[i];}"
                + "var rows=document.querySelectorAll('.dl-list li[data-id]');"
                + "for(var j=0;j<rows.length;j++){var row=rows[j];var it=map[row.getAttribute('data-id')];"
                + "if(!it){continue;}var s=row.querySelector('.dl-status');if(s){s.textContent=it.text;}"
                + "var b=row.querySelector('.dl-bar i');if(b){b.style.width=(it.percent>0?it.percent:0)+'%';}}}");
            body.Append("setInterval(function(){post({cmd:'downloadProgress'});},700);");
            body.Append("if(window.chrome&&window.chrome.webview){window.chrome.webview.addEventListener('message',function(e){"
                + "var d=e.data;if(typeof d!=='string'||d.charAt(0)!=='{'){return;}"
                + "try{var o=JSON.parse(d);if(o&&o.items){applyStatus(o.items);}}catch(x){}});}");
            body.Append("</script>");

            return Page("下载", "共 " + total + " 条", body.ToString());
        }

        private static void AppendDownloadIcon(StringBuilder sb, string action, string id, string title, string svg)
        {
            sb.Append("<button type=\"button\" class=\"icon-btn\" title=\"").Append(Escape(title))
                .Append("\" data-action=\"").Append(Escape(action))
                .Append("\" data-id=\"").Append(id).Append("\">")
                .Append(svg).Append("</button>");
        }

        internal static string DescribeDownload(DownloadItem item)
        {
            if (item.IsCompleted)
            {
                return "已完成 · " + FormatBytes(item.TotalBytes > 0 ? item.TotalBytes : item.BytesReceived);
            }

            if (string.Equals(item.State, DownloadItem.StatePaused, StringComparison.Ordinal))
            {
                return "已暂停 · " + FormatProgress(item);
            }

            if (string.Equals(item.State, DownloadItem.StateCancelled, StringComparison.Ordinal))
            {
                return "已取消";
            }

            if (item.IsFailed)
            {
                return string.IsNullOrEmpty(item.Error) ? "已中断" : "已中断 · " + item.Error;
            }

            return "下载中 · " + FormatProgress(item);
        }

        private static int DownloadPercent(DownloadItem item)
        {
            if (item.TotalBytes > 0)
            {
                return (int)Math.Round(item.Progress * 100);
            }

            return item.IsCompleted ? 100 : 0;
        }

        private static string FormatProgress(DownloadItem item)
        {
            if (item.TotalBytes > 0)
            {
                int percent = (int)Math.Round(item.Progress * 100);
                return FormatBytes(item.BytesReceived) + " / " + FormatBytes(item.TotalBytes) + " (" + percent + "%)";
            }

            return FormatBytes(item.BytesReceived);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? ((long)value) + " " + units[unit]
                : value.ToString("0.0") + " " + units[unit];
        }

        private const string IconOpen = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\"><path d=\"M5 3h7l4 4v14H5z\"/><path d=\"M12 3v4h4\"/></svg>";
        private const string IconFolder = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\"><path d=\"M3 6a1 1 0 0 1 1-1h5l2 2h8a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z\"/></svg>";
        private const string IconDelete = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\"><path d=\"M4 7h16\"/><path d=\"M9 7V4h6v3\"/><path d=\"M6 7l1 13h10l1-13\"/><path d=\"M10 11v6M14 11v6\"/></svg>";
        private const string IconPause = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\"><path d=\"M9 5v14M15 5v14\"/></svg>";
        private const string IconPlay = "<svg viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M8 5l11 7-11 7z\"/></svg>";
        private const string IconCancel = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg>";
        private const string IconRetry = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M20 12a8 8 0 1 1-2.34-5.66\"/><path d=\"M20 4v5h-5\"/></svg>";

        private static void AppendHistoryFilter(StringBuilder sb, string key, string label)
        {
            sb.Append("<button class=\"filter\" data-range=\"").Append(key)
                .Append("\" onclick=\"setRange('").Append(key).Append("')\">")
                .Append(Escape(label)).Append("</button>");
        }

        public static string NewTab(AppSettings settings, BookmarkStore store)
        {
            if (settings == null)
            {
                settings = AppSettings.CreateDefault();
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"newtab\">");
            body.Append("<div class=\"nt-clock\" id=\"ntClock\">--:--</div>");
            body.Append("<div class=\"nt-date\" id=\"ntDate\"></div>");
            body.Append("<form class=\"nt-search\" onsubmit=\"return ntSearch(event)\">");
            body.Append("<select id=\"ntEngine\" class=\"nt-engine\" onchange=\"ntEngineChanged()\" title=\"搜索引擎\">");
            AppendSearchOption(body, settings.SearchEngine, "bing", "必应");
            AppendSearchOption(body, settings.SearchEngine, "baidu", "百度");
            AppendSearchOption(body, settings.SearchEngine, "google", "Google");
            AppendSearchOption(body, settings.SearchEngine, "duckduckgo", "DuckDuckGo");
            AppendCustomEngineOptions(body, settings);
            body.Append("</select>");
            body.Append("<input id=\"ntQuery\" type=\"text\" placeholder=\"搜索或输入网址\" autocomplete=\"off\" autofocus>");
            body.Append("<button class=\"nt-go\" type=\"submit\" title=\"搜索\">")
                .Append("<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" ")
                .Append("stroke-linecap=\"round\"><circle cx=\"11\" cy=\"11\" r=\"7\"/>")
                .Append("<line x1=\"16.5\" y1=\"16.5\" x2=\"21\" y2=\"21\"/></svg></button>");
            body.Append("</form>");

            if (settings.NewTabShortcuts)
            {
                AppendShortcuts(body, store);
            }

            body.Append("</div>");

            body.Append("<script>");
            body.Append("function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}");
            body.Append("var NT_ENGINES={");
            bool firstEngine = true;
            AppendEngineJs(body, ref firstEngine, "bing", "https://www.bing.com/search?q={query}");
            AppendEngineJs(body, ref firstEngine, "baidu", "https://www.baidu.com/s?wd={query}");
            AppendEngineJs(body, ref firstEngine, "google", "https://www.google.com/search?q={query}");
            AppendEngineJs(body, ref firstEngine, "duckduckgo", "https://duckduckgo.com/?q={query}");

            if (settings.CustomSearchEngines != null)
            {
                for (int i = 0; i < settings.CustomSearchEngines.Count; i++)
                {
                    CustomSearchEngine engine = settings.CustomSearchEngines[i];
                    if (engine != null && !string.IsNullOrWhiteSpace(engine.Name) && !string.IsNullOrWhiteSpace(engine.Url))
                    {
                        AppendEngineJs(body, ref firstEngine, UrlHelper.EngineKey(engine), engine.Url);
                    }
                }
            }

            body.Append("};");
            body.Append("function ntEngineChanged(){post({cmd:'searchEngine',search:document.getElementById('ntEngine').value});}");
            body.Append("function ntTarget(q){"
                + "if(/^(https?:\\/\\/|about:|file:)/i.test(q)){return q;}"
                + "if(/^[^\\s]+\\.[^\\s]+$/.test(q)){return 'https://'+q;}"
                + "var t=NT_ENGINES[document.getElementById('ntEngine').value]||NT_ENGINES['bing'];"
                + "return t.replace('{query}',encodeURIComponent(q));}");
            body.Append("function ntSearch(ev){ev.preventDefault();"
                + "var q=document.getElementById('ntQuery').value.trim();"
                + "if(!q){return false;}post({cmd:'navigate',address:ntTarget(q)});return false;}");
            body.Append("function ntTick(){var d=new Date();"
                + "var hh=('0'+d.getHours()).slice(-2);var mm=('0'+d.getMinutes()).slice(-2);"
                + "document.getElementById('ntClock').textContent=hh+':'+mm;"
                + "var w=['星期日','星期一','星期二','星期三','星期四','星期五','星期六'];"
                + "document.getElementById('ntDate').textContent=d.getFullYear()+'年'+(d.getMonth()+1)+'月'+d.getDate()+'日 '+w[d.getDay()];}");
            body.Append("ntTick();setInterval(ntTick,1000);");
            body.Append("</script>");

            return Page("新标签页", string.Empty, body.ToString(), true);
        }

        private static void AppendShortcuts(StringBuilder sb, BookmarkStore store)
        {
            BookmarkNode toolbar = store == null ? null : store.ToolbarFolder;
            IList<BookmarkNode> children = toolbar == null ? null : toolbar.GetChildren();
            const int max = 8;

            sb.Append("<div class=\"shortcuts\">");

            int count = 0;
            if (children != null)
            {
                for (int i = 0; i < children.Count && count < max; i++)
                {
                    BookmarkNode child = children[i];
                    if (child.IsFolder || string.IsNullOrWhiteSpace(child.Url))
                    {
                        continue;
                    }

                    string title = string.IsNullOrWhiteSpace(child.Title) ? child.Url : child.Title;
                    string data = FaviconCache.GetDataUri(child.Url);

                    sb.Append("<a class=\"shortcut\" href=\"").Append(Escape(child.Url))
                        .Append("\" target=\"_self\" title=\"").Append(Escape(title)).Append("\">");

                    if (!string.IsNullOrEmpty(data))
                    {
                        sb.Append("<img class=\"sc-icon\" src=\"").Append(data).Append("\" alt=\"\">");
                    }
                    else
                    {
                        sb.Append("<span class=\"sc-icon\">").Append(Escape(FirstLetter(title))).Append("</span>");
                    }

                    sb.Append("<span class=\"sc-title\">").Append(Escape(title)).Append("</span></a>");
                    count++;
                }
            }

            if (count == 0)
            {
                sb.Append("<span class=\"hint\">书签工具栏为空，按 Ctrl+D 添加书签后会显示在这里。</span>");
            }

            sb.Append("</div>");
        }

        private static string FirstLetter(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "?";
            }

            return text.Substring(0, 1).ToUpperInvariant();
        }

        private static void AppendNewTabOption(StringBuilder sb, string current, string value, string label)
        {
            sb.Append("<option value=\"").Append(value).Append("\"");
            if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(Escape(label)).Append("</option>");
        }

        private static void AppendCustomEngineRow(StringBuilder sb, CustomSearchEngine engine)
        {
            sb.Append("<div class=\"engine-row\"><input class=\"ce-name\" type=\"text\" placeholder=\"名称\" value=\"")
                .Append(Escape(engine == null ? string.Empty : engine.Name))
                .Append("\" oninput=\"refreshEngineSelect();scheduleSave()\">")
                .Append("<input class=\"ce-url\" type=\"text\" placeholder=\"https://example.com/search?q={query}\" value=\"")
                .Append(Escape(engine == null ? string.Empty : engine.Url))
                .Append("\" oninput=\"scheduleSave()\"><button class=\"danger\" onclick=\"removeEngineRow(this)\">删除</button></div>");
        }

        private static void AppendCustomEngineOptions(StringBuilder sb, AppSettings settings)
        {
            if (settings.CustomSearchEngines == null)
            {
                return;
            }

            for (int i = 0; i < settings.CustomSearchEngines.Count; i++)
            {
                CustomSearchEngine engine = settings.CustomSearchEngines[i];

                if (engine != null && !string.IsNullOrWhiteSpace(engine.Name))
                {
                    AppendSearchOption(sb, settings.SearchEngine, UrlHelper.EngineKey(engine), engine.Name);
                }
            }
        }

        private static void AppendEngineJs(StringBuilder sb, ref bool first, string key, string template)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(Json(key)).Append(':').Append(Json(template));
        }

        private static void AppendSearchOption(StringBuilder sb, string current, string value, string label)
        {
            sb.Append("<option value=\"").Append(value).Append("\"");
            if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(Escape(label)).Append("</option>");
        }

        public static string Preferences(AppSettings settings)
        {
            if (settings == null)
            {
                settings = AppSettings.CreateDefault();
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"settings\">");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>启动</h2>")
                .Append("<p>浏览器启动时打开的页面</p></div><div class=\"card-body\">");
            string homeMode = string.IsNullOrWhiteSpace(settings.HomeMode)
                ? AppSettings.DefaultHomeMode
                : settings.HomeMode;

            body.Append("<div class=\"field-row\"><span class=\"label\">启动方式</span><span class=\"control\">")
                .Append("<select id=\"homeMode\" onchange=\"syncHomeFields()\">");
            AppendThemeOption(body, homeMode, "asys", "ASYS 搜索起始页");
            AppendThemeOption(body, homeMode, "blank", "空白页");
            AppendThemeOption(body, homeMode, "newtab", "Vantage Lite 新标签页");
            AppendThemeOption(body, homeMode, "custom", "自定义页面");
            body.Append("</select></span></div>");
            body.Append("<div class=\"field-row\" id=\"homeUrlRow\"><span class=\"label\">自定义网址</span>")
                .Append("<span class=\"control\"><input id=\"home\" type=\"text\" value=\"")
                .Append(Escape(settings.HomeUrl)).Append("\">")
                .Append("<span class=\"hint\">仅“自定义页面”时使用</span></span></div>");
            body.Append("<label class=\"check\"><input id=\"restoreSession\" type=\"checkbox\"")
                .Append(settings.RestoreSession ? " checked" : string.Empty)
                .Append("> 启动时恢复上次标签页</label>");
            body.Append("<span class=\"hint\">与「关闭时清空」互斥；开启清空后不会恢复会话</span>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>新标签页</h2>")
                .Append("<p>Ctrl+T 和 + 按钮打开的内容</p></div><div class=\"card-body\">");
            body.Append("<div class=\"field-row\"><span class=\"label\">打开方式</span><span class=\"control\">")
                .Append("<select id=\"newTabMode\" onchange=\"syncNewTabFields()\">");
            AppendNewTabOption(body, settings.NewTabMode, "builtin", "Vantage Lite 新标签页");
            AppendNewTabOption(body, settings.NewTabMode, "blank", "空白页");
            AppendNewTabOption(body, settings.NewTabMode, "home", "使用当前主页");
            AppendNewTabOption(body, settings.NewTabMode, "custom", "自定义页面");
            body.Append("</select></span></div>");
            body.Append("<div class=\"field-row\" id=\"newTabUrlRow\"><span class=\"label\">自定义网址</span>")
                .Append("<span class=\"control\"><input id=\"newTabUrl\" type=\"text\" value=\"")
                .Append(Escape(settings.NewTabUrl)).Append("\">")
                .Append("<span class=\"hint\">仅“自定义页面”时使用，留空则回退到内置页</span></span></div>");
            body.Append("<div class=\"field-row\" id=\"newTabShortcutRow\"><span class=\"label\">快捷入口</span>")
                .Append("<span class=\"control\"><label class=\"check\"><input id=\"ntShortcuts\" type=\"checkbox\"")
                .Append(settings.NewTabShortcuts ? " checked" : string.Empty)
                .Append("> 显示书签快捷入口</label>")
                .Append("<span class=\"hint\">仅“Vantage Lite 新标签页”时生效</span></span></div>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>性能</h2>")
                .Append("<p>控制后台标签占用的内存</p></div><div class=\"card-body\">");
            AppendNumberRow(body, "maxTabs", "最大存活标签页", settings.MaxLiveTabs, 1, 50,
                "同时保留的 WebView2 实例上限");
            AppendNumberRow(body, "suspend", "后台挂起等待（秒）", settings.SuspendAfterSeconds, 0, 3600,
                "0 表示不自动挂起");
            AppendNumberRow(body, "discard", "挂起后销毁等待（分钟）", settings.DiscardAfterMinutes, 0, 1440,
                "0 表示不自动销毁");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>搜索</h2>")
                .Append("<p>地址栏与新标签页使用的搜索引擎</p></div><div class=\"card-body\">");
            body.Append("<div class=\"field-row\"><span class=\"label\">搜索引擎</span><span class=\"control\">")
                .Append("<select id=\"search\">");
            AppendSearchOption(body, settings.SearchEngine, "bing", "必应");
            AppendSearchOption(body, settings.SearchEngine, "baidu", "百度");
            AppendSearchOption(body, settings.SearchEngine, "google", "Google");
            AppendSearchOption(body, settings.SearchEngine, "duckduckgo", "DuckDuckGo");
            AppendCustomEngineOptions(body, settings);
            body.Append("</select>");
            body.Append("<span class=\"hint\">自定义引擎可在下方卡片中添加</span></span></div>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>自定义搜索引擎</h2>")
                .Append("<p>名称 + 搜索 URL，用 {query} 代表关键词</p></div><div class=\"card-body\">");
            body.Append("<div id=\"customEngines\">");

            if (settings.CustomSearchEngines != null)
            {
                for (int i = 0; i < settings.CustomSearchEngines.Count; i++)
                {
                    AppendCustomEngineRow(body, settings.CustomSearchEngines[i]);
                }
            }

            body.Append("</div>");
            body.Append("<div><button onclick=\"addCustomEngine()\">＋ 添加搜索引擎</button></div>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>下载</h2>")
                .Append("<p>文件保存位置；下载面板由 WebView2 提供</p></div><div class=\"card-body\">");
            body.Append("<div class=\"field-row\"><span class=\"label\">保存目录</span>")
                .Append("<span class=\"control dir-row\"><input id=\"downloadDir\" type=\"text\" value=\"")
                .Append(Escape(settings.DownloadDirectory))
                .Append("\" placeholder=\"留空则使用系统下载文件夹\">")
                .Append("<button onclick=\"pickDownloadDir()\">浏览…</button></span></div>");
            body.Append("<span class=\"hint\">留空则使用系统默认下载文件夹；修改后新下载立即生效</span>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>界面</h2>")
                .Append("<p>主题与工具栏</p></div><div class=\"card-body\">");
            body.Append("<div class=\"field-row\"><span class=\"label\">主题</span><span class=\"control\">")
                .Append("<select id=\"themeMode\" onchange=\"applyThemeNow()\">");
            AppendThemeOption(body, settings.ThemeMode, "system", "跟随系统");
            AppendThemeOption(body, settings.ThemeMode, "light", "浅色");
            AppendThemeOption(body, settings.ThemeMode, "dark", "深色");
            body.Append("</select><span class=\"hint\">跟随系统会随 Windows 应用模式自动切换</span></span></div>");
            body.Append("<label class=\"check\"><input id=\"bar\" type=\"checkbox\"")
                .Append(settings.ShowBookmarksBar ? " checked" : string.Empty)
                .Append("> 显示书签栏</label>");
            body.Append("</div></section>");

            body.Append("<section class=\"card\"><div class=\"card-head\"><h2>隐私</h2>")
                .Append("<p>Cookie 与浏览痕迹</p></div><div class=\"card-body\">");
            body.Append("<label class=\"check\"><input id=\"clearOnExit\" type=\"checkbox\"")
                .Append(settings.ClearOnExit ? " checked" : string.Empty)
                .Append("> 每次关闭时清空 Cookie 和浏览记录</label>");
            body.Append("<div class=\"button-row\">")
                .Append("<button onclick=\"askAction('清理所有 Cookie','清理后所有网站将退出登录。','clearCookies')\">清理所有 Cookie</button>")
                .Append("<button onclick=\"askAction('清空浏览记录','将删除全部本地历史记录。','clearHistory')\">清空浏览记录</button>")
                .Append("<button class=\"danger\" onclick=\"askAction('清空所有使用痕迹','将清除 Cookie、缓存、历史等数据；书签和设置会保留。','clearAllData')\">清空所有使用痕迹</button>")
                .Append("</div>");
            body.Append("</div></section>");

            body.Append("</div>");

            body.Append("<div id=\"actionBox\" class=\"modal\"><div class=\"panel\">")
                .Append("<div class=\"panel-title\" id=\"actionTitle\"></div>")
                .Append("<p id=\"actionText\" class=\"hint\" style=\"margin:8px 0 0\"></p>")
                .Append("<div class=\"panel-buttons\"><button class=\"danger\" onclick=\"actionConfirm()\">确定</button>")
                .Append("<button onclick=\"hideAction()\">取消</button></div></div></div>");

            body.Append("<div class=\"savebar\"><span class=\"hint\">更改会自动保存</span>")
                .Append("<span id=\"status\" class=\"status\"></span></div>");

            body.Append("<script>");
            body.Append("function post(o){try{window.chrome.webview.postMessage(JSON.stringify(o));}catch(e){}}");
            body.Append("var pendingCmd='';");
            body.Append("function askAction(title,text,cmd){pendingCmd=cmd;"
                + "document.getElementById('actionTitle').textContent=title;"
                + "document.getElementById('actionText').textContent=text;"
                + "document.getElementById('actionBox').style.display='flex';}");
            body.Append("function hideAction(){document.getElementById('actionBox').style.display='none';pendingCmd='';}");
            body.Append("function actionConfirm(){var c=pendingCmd;hideAction();if(c){post({cmd:c});}}");
            body.Append("var BUILTIN_ENGINES=[['bing','必应'],['baidu','百度'],['google','Google'],['duckduckgo','DuckDuckGo']];");
            body.Append("function applyThemeNow(){post({cmd:'theme',theme:document.getElementById('themeMode').value});}");
            body.Append("function addCustomEngine(){var box=document.getElementById('customEngines');"
                + "var row=document.createElement('div');row.className='engine-row';"
                + "row.innerHTML='<input class=\"ce-name\" type=\"text\" placeholder=\"名称\" oninput=\"refreshEngineSelect();scheduleSave()\">"
                + "<input class=\"ce-url\" type=\"text\" placeholder=\"https://example.com/search?q={query}\" oninput=\"scheduleSave()\">"
                + "<button class=\"danger\" onclick=\"removeEngineRow(this)\">删除</button>';"
                + "box.appendChild(row);refreshEngineSelect();}");
            body.Append("function removeEngineRow(btn){btn.parentNode.parentNode.removeChild(btn.parentNode);refreshEngineSelect();scheduleSave();}");
            body.Append("function refreshEngineSelect(){var sel=document.getElementById('search');var cur=sel.value;sel.innerHTML='';"
                + "for(var i=0;i<BUILTIN_ENGINES.length;i++){var o=document.createElement('option');o.value=BUILTIN_ENGINES[i][0];o.textContent=BUILTIN_ENGINES[i][1];sel.appendChild(o);}"
                + "var rows=document.querySelectorAll('#customEngines .engine-row');"
                + "for(var j=0;j<rows.length;j++){var n=rows[j].querySelector('.ce-name').value.trim();if(!n){continue;}"
                + "var c=document.createElement('option');c.value='custom:'+n;c.textContent=n;sel.appendChild(c);}"
                + "if(cur){sel.value=cur;}if(!sel.value&&sel.options.length){sel.selectedIndex=0;}}");
            body.Append("refreshEngineSelect();");
            body.Append("function syncHomeFields(){"
                + "var m=document.getElementById('homeMode').value;"
                + "document.getElementById('homeUrlRow').style.display=(m==='custom')?'flex':'none';}");
            body.Append("syncHomeFields();");
            body.Append("function syncNewTabFields(){"
                + "var m=document.getElementById('newTabMode').value;"
                + "document.getElementById('newTabUrlRow').style.display=(m==='custom')?'flex':'none';"
                + "document.getElementById('newTabShortcutRow').style.display=(m==='builtin')?'flex':'none';}");
            body.Append("syncNewTabFields();");
            body.Append("function pickDownloadDir(){post({cmd:'pickDownloadDir'});}");
            body.Append("var saveTimer=null;"
                + "function scheduleSave(){if(saveTimer){clearTimeout(saveTimer);}saveTimer=setTimeout(saveSettings,300);}");
            body.Append("(function(){var els=document.querySelectorAll('.settings input,.settings select');"
                + "for(var i=0;i<els.length;i++){var el=els[i];"
                + "if(el.id==='themeMode'){continue;}"
                + "el.addEventListener('change',saveSettings);"
                + "if((el.type==='text'||el.type==='number')&&el.id!=='downloadDir'){el.addEventListener('input',scheduleSave);}}})();");
            body.Append("var restoreBox=document.getElementById('restoreSession');"
                + "var clearBox=document.getElementById('clearOnExit');"
                + "restoreBox.addEventListener('change',function(){if(this.checked){clearBox.checked=false;}saveSettings();});"
                + "clearBox.addEventListener('change',function(){if(this.checked){restoreBox.checked=false;}saveSettings();});");
            body.Append("function saveSettings(){"
                + "var homeMode=document.getElementById('homeMode').value;"
                + "var home=document.getElementById('home').value.trim();"
                + "var maxTabs=parseInt(document.getElementById('maxTabs').value,10);"
                + "if(!(maxTabs>=1&&maxTabs<=50)){document.getElementById('status').textContent='标签页数量需在 1-50 之间';return;}"
                + "var suspend=parseInt(document.getElementById('suspend').value,10);"
                + "if(!(suspend>=0&&suspend<=3600)){document.getElementById('status').textContent='挂起等待需在 0-3600 秒之间';return;}"
                + "var discard=parseInt(document.getElementById('discard').value,10);"
                + "if(!(discard>=0&&discard<=1440)){document.getElementById('status').textContent='销毁等待需在 0-1440 分钟之间';return;}"
                + "var search=document.getElementById('search').value;"
                + "var themeMode=document.getElementById('themeMode').value;"
                + "var clearOnExit=document.getElementById('clearOnExit').checked;"
                + "var restoreSession=document.getElementById('restoreSession').checked;"
                + "var newTabMode=document.getElementById('newTabMode').value;"
                + "var newTabUrl=document.getElementById('newTabUrl').value.trim();"
                + "var ntShortcuts=document.getElementById('ntShortcuts').checked;"
                + "var bar=document.getElementById('bar').checked;"
                + "var downloadDir=document.getElementById('downloadDir').value.trim();"
                + "var engines=[];var rows=document.querySelectorAll('#customEngines .engine-row');"
                + "for(var i=0;i<rows.length;i++){var n=rows[i].querySelector('.ce-name').value.trim();"
                + "var u=rows[i].querySelector('.ce-url').value.trim();if(n&&u){engines.push({name:n,url:u});}}"
                + "post({cmd:'settings',homeMode:homeMode,home:home,customEngines:engines,maxTabs:maxTabs,suspendSeconds:suspend,discardMinutes:discard,search:search,theme:themeMode,clearOnExit:clearOnExit,restoreSession:restoreSession,newTabMode:newTabMode,newTabUrl:newTabUrl,newTabShortcuts:ntShortcuts,bookmarksBar:bar,downloadDir:downloadDir});"
                + "}");
            body.Append("if(window.chrome&&window.chrome.webview){"
                + "window.chrome.webview.addEventListener('message',function(e){"
                + "var s=document.getElementById('status');var d=e.data;"
                + "if(typeof d==='string'&&d.charAt(0)==='{'){try{var o=JSON.parse(d);"
                + "if(o&&o.downloadDir!==undefined){document.getElementById('downloadDir').value=o.downloadDir;return;}}catch(x){}}"
                + "if(s){s.textContent=d||'';}});}");
            body.Append("</script>");

            return Page("设置", "Vantage Lite 基础选项", body.ToString());
        }

        private static void AppendTextRow(StringBuilder sb, string id, string label, string value, string hint)
        {
            sb.Append("<div class=\"field-row\"><span class=\"label\">").Append(Escape(label)).Append("</span>")
                .Append("<span class=\"control\"><input id=\"").Append(id)
                .Append("\" type=\"text\" value=\"").Append(Escape(value)).Append("\">")
                .Append("<span class=\"hint\">").Append(Escape(hint)).Append("</span></span></div>");
        }

        private static void AppendNumberRow(StringBuilder sb, string id, string label, int value, int min, int max, string hint)
        {
            sb.Append("<div class=\"field-row\"><span class=\"label\">").Append(Escape(label)).Append("</span>")
                .Append("<span class=\"control\"><input id=\"").Append(id)
                .Append("\" type=\"number\" min=\"").Append(min)
                .Append("\" max=\"").Append(max)
                .Append("\" value=\"").Append(value).Append("\">")
                .Append("<span class=\"hint\">").Append(Escape(hint)).Append("</span></span></div>");
        }

        private static void AppendThemeOption(StringBuilder sb, string current, string value, string label)
        {
            sb.Append("<option value=\"").Append(value).Append("\"");
            if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" selected");
            }

            sb.Append(">").Append(Escape(label)).Append("</option>");
        }

        public static string About(string version, string runtime)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"about\">");
            AppendLine(body, "版本", string.IsNullOrEmpty(version) ? "unknown" : version);
            AppendLine(body, "WebView2 Runtime", string.IsNullOrEmpty(runtime) ? "未检测到" : runtime);
            AppendLine(body, "数据目录", AppPaths.DataDirectory);
            AppendLine(body, "定位", "轻量、可控的 Windows 浏览器");
            AppendLine(body, "诊断信息", "菜单 → 诊断信息，或在地址栏输入 about:diag");
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
            body.Append("<input id=\"bmQuery\" class=\"search\" type=\"text\" placeholder=\"搜索书签\" oninput=\"applyBookmarkSearch()\">");
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
                        .Append("',event)\">")
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
  ul.setAttribute('data-open',open?'0':'1');
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
            sb.Append(@"
function childTree(li){
  for(var i=0;i<li.children.length;i++){if(li.children[i].tagName==='UL'){return li.children[i];}}
  return null;
}
function snapshotTree(){
  var uls=document.querySelectorAll('ul.tree');
  for(var i=0;i<uls.length;i++){uls[i].setAttribute('data-open',uls[i].style.display==='none'?'0':'1');}
}
function applyBookmarkSearch(){
  var input=document.getElementById('bmQuery');
  if(!input){return;}
  var top=document.querySelector('ul.tree');
  if(!top){return;}
  var q=input.value.trim().toLowerCase();
  if(!q){restoreTree();return;}
  filterTree(top,q);
}
function filterTree(ul,q){
  var any=false;
  for(var i=0;i<ul.children.length;i++){
    var li=ul.children[i];
    if(li.tagName!=='LI'){continue;}
    var childUl=childTree(li);
    var head=li.querySelector('.row')||li.querySelector('.folder-head');
    var text=head?head.textContent.toLowerCase():'';
    if(!childUl){
      var u=li.querySelector('.url');
      if(u){text+=u.textContent.toLowerCase();}
    }
    var own=text.indexOf(q)>=0;
    var child=childUl?filterTree(childUl,q):false;
    var show=own||child;
    li.style.display=show?'':'none';
    if(childUl){
      childUl.style.display=child?'block':'none';
      var tri=li.querySelector('.tri');
      if(tri){tri.textContent=child?'▾':'▸';}
    }
    if(show){any=true;}
  }
  return any;
}
function restoreTree(){
  var uls=document.querySelectorAll('ul.tree');
  for(var i=0;i<uls.length;i++){uls[i].style.display=uls[i].getAttribute('data-open')==='0'?'none':'block';}
  var lis=document.querySelectorAll('ul.tree li');
  for(var j=0;j<lis.length;j++){lis[j].style.display='';}
  var tris=document.querySelectorAll('li.folder .tri');
  for(var k=0;k<tris.length;k++){
    var li=tris[k].closest('li.folder');
    var ul=li?childTree(li):null;
    if(ul){tris[k].textContent=ul.style.display==='none'?'▸':'▾';}
  }
}
snapshotTree();
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
                    return "<img class=\"fav\" loading=\"lazy\" decoding=\"async\" src=\"" + shell + "\">";
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
                return "<img class=\"fav\" loading=\"lazy\" decoding=\"async\" src=\"" + data + "\">";
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

            return "<img class=\"fav\" loading=\"lazy\" decoding=\"async\" src=\"" + Escape(origin)
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
            return Page(title, subtitle, body, false);
        }

        private static string Page(string title, string subtitle, string body, bool hideHeader)
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
                + ";--btn:" + Hex(palette.ButtonHover)
                + ";--card:" + Hex(palette.ChromeBackground)
                + ";--input:" + Hex(palette.AddressBackground) + "}"
                + ":focus{outline:none}"
                + "a:focus{text-decoration:underline}"
                + "input:focus,select:focus{border-color:var(--link)}"
                + "body{margin:0;padding:28px 36px;background:var(--bg);color:var(--fg);"
                + "font:14px/1.6 \"Segoe UI\",\"Microsoft YaHei\",sans-serif}"
                + "h1{font-size:18px;font-weight:600;margin:0 0 4px}"
                + ".sub{color:var(--muted);font-size:12px;margin-bottom:20px}"
                + "ul{list-style:none;padding:0;margin:0}"
                + "li{padding:6px 0;border-bottom:1px solid var(--border)}"
                + "a{color:var(--link);text-decoration:none}"
                + "a:hover{text-decoration:underline}"
                + ".url{color:var(--muted);font-size:12px;word-break:break-all}"
                + ".name,.url,.dl-name{overflow-wrap:anywhere}"
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
                + "background:var(--btn);color:var(--fg);border:1px solid var(--border);border-radius:4px;color-scheme:inherit}"
                + "select option{background:var(--bg);color:var(--fg)}"
                + ".warn{background:#c0392b;color:#fff;padding:8px 12px;border-radius:4px;margin-bottom:12px}"
                + ".panel-buttons{margin-top:14px;display:flex;gap:8px;justify-content:flex-end}"
                + ".newtab{max-width:760px;margin:12vh auto 0;padding:0 8px;text-align:center}"
                + ".nt-clock{font-size:56px;font-weight:300;letter-spacing:3px}"
                + ".nt-date{color:var(--muted);font-size:13px;margin-top:6px}"
                + ".nt-search{display:flex;align-items:center;gap:6px;max-width:600px;margin:30px auto 0;"
                + "padding:6px 8px 6px 12px;border:1px solid var(--border);border-radius:12px;background:var(--input)}"
                + ".nt-search:focus-within{border-color:var(--link)}"
                + ".nt-engine{border:none;background:transparent;color:var(--muted);font-size:13px;outline:none;max-width:112px}"
                + ".nt-search input{flex:1;min-width:0;border:none;background:transparent;color:var(--fg);font-size:15px;"
                + "outline:none;padding:6px 2px}"
                + ".nt-go{width:34px;height:34px;flex:0 0 34px;border:none;background:transparent;color:var(--muted);"
                + "display:flex;align-items:center;justify-content:center;cursor:pointer;border-radius:8px;padding:0}"
                + ".nt-go:hover{background:var(--btn);color:var(--fg)}"
                + ".nt-go svg{width:18px;height:18px}"
                + ".shortcuts{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;max-width:720px;margin:34px auto 0}"
                + ".shortcut{display:flex;align-items:center;gap:9px;padding:10px 12px;border:1px solid var(--border);"
                + "border-radius:10px;background:var(--card);color:var(--fg);text-decoration:none;font-size:13px;min-width:0}"
                + ".shortcut:hover{border-color:var(--link);text-decoration:none}"
                + ".shortcut .sc-title{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}"
                + ".sc-icon{width:22px;height:22px;flex:0 0 22px;border-radius:5px;object-fit:contain;background:var(--btn)}"
                + "span.sc-icon{display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:600;color:var(--muted)}"
                + "@media (max-width:640px){.shortcuts{grid-template-columns:repeat(2,minmax(0,1fr))}}"
                + ".filters{display:flex;align-items:center;gap:6px;margin-bottom:14px;flex-wrap:wrap}"
                + ".search{width:220px;flex:0 0 auto}"
                + ".settings{max-width:780px;margin:0 auto}"
                + ".page{max-width:900px;width:100%;margin:0 auto;box-sizing:border-box}"
                + ".card{border:1px solid var(--border);border-radius:10px;background:var(--card);margin-bottom:14px}"
                + ".card-head{padding:14px 18px 0}"
                + ".card-head h2{font-size:15px;font-weight:600;margin:0}"
                + ".card-head p{margin:4px 0 0;color:var(--muted);font-size:12px}"
                + ".card-body{padding:14px 18px 18px;display:flex;flex-direction:column;gap:14px}"
                + ".field-row{display:flex;align-items:center;gap:16px}"
                + ".field-row>.label{width:190px;flex:0 0 190px;font-size:13px}"
                + ".field-row>.control{flex:1;min-width:0}"
                + ".field-row input[type=text],.field-row input[type=number],.field-row select{width:100%}"
                + ".hint{display:block;color:var(--muted);font-size:12px;margin-top:4px}"
                + ".savebar{display:flex;align-items:center;gap:12px;padding:6px 0 24px}"
                + ".savebar .status{margin-left:0}"
                + ".button-row{display:flex;gap:8px;flex-wrap:wrap}"
                + ".engine-row{display:flex;gap:8px;align-items:center;margin-bottom:8px}"
                + ".engine-row input{flex:1;min-width:0}"
                + ".engine-row button{flex:0 0 auto}"
                + ".dir-row{display:flex;gap:8px;align-items:center}"
                + ".dir-row input[type=text]{flex:1;width:auto;min-width:0}"
                + ".dir-row button{flex:0 0 auto}"
                + ".card-body .check{margin:0}"
                + "button.primary{background:var(--link);border-color:var(--link);color:#ffffff;font-weight:600}"
                + "@media (max-width:640px){.field-row{flex-direction:column;align-items:stretch;gap:6px}"
                + ".field-row>.label{width:auto;flex:none}}"
                + ".filters .status{margin-left:auto;margin-right:4px}"
                + ".filter.active{background:var(--link);border-color:var(--link);color:#ffffff}"
                + "button.danger{color:#d9534f;border-color:#d9534f}"
                + "</style><script>var VLT_THEME=" + ThemeJson() + ";"
                + "function vantageApplyTheme(dark){var t=VLT_THEME[dark?'dark':'light'];if(!t){return;}"
                + "var s=document.documentElement.style;s.setProperty('color-scheme',dark?'dark':'light');"
                + "s.setProperty('--bg',t.bg);s.setProperty('--fg',t.fg);s.setProperty('--muted',t.muted);"
                + "s.setProperty('--border',t.border);s.setProperty('--link',t.link);s.setProperty('--btn',t.btn);"
                + "s.setProperty('--card',t.card);s.setProperty('--input',t.input);}"
                + "</script></head><body><div class=\"page\">"
                + (hideHeader
                    ? string.Empty
                    : "<h1>" + Escape(title) + "</h1><div class=\"sub\">" + Escape(subtitle) + "</div>")
                + body + "</div></body></html>";
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
                + "',btn:'" + Hex(palette.ButtonHover)
                + "',card:'" + Hex(palette.ChromeBackground)
                + "',input:'" + Hex(palette.AddressBackground) + "'}";
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
