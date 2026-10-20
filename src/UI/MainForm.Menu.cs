using System;
using System.Windows.Forms;
using VantageLite.Theming;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的汉堡菜单：基础导航与内置页面入口。
    /// 依赖：ThemeFonts / 其他 partial。改动注意：所有菜单项复用现有方法，不直接操作 Store。
    /// </summary>
    public sealed partial class MainForm
    {
        private void ShowMainMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = ThemeFonts.Ui(9.5F);

            menu.Items.Add(AddItem("新建标签页", delegate { CreateNewTab(); }));
            menu.Items.Add(AddItem("历史", delegate { ShowInternalPage(HistoryAddress); }));
            menu.Items.Add(AddItem("书签管理器", delegate { ShowBookmarkManager(); }));
            menu.Items.Add(AddItem(
                _bookmarksBar.Visible ? "隐藏书签栏" : "显示书签栏",
                delegate { ToggleBookmarksBar(); }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AddItem("关于 Vantage Lite", delegate { ShowInternalPage(AboutAddress); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AddItem("退出", delegate { Close(); }));

            menu.Closed += delegate
            {
                if (IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(menu.Dispose));
                        return;
                    }
                    catch
                    {
                    }
                }

                menu.Dispose();
            };

            menu.Show(Cursor.Position);
        }

        private static ToolStripMenuItem AddItem(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { action(); };
            return item;
        }
    }
}
