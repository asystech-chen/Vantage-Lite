using System.Windows.Forms;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的快捷键处理（partial）。
    /// 依赖：TabManager。改动注意：新增快捷键时避免与浏览器页面内快捷键冲突。
    /// </summary>
    public sealed partial class MainForm
    {
        private async void ActivateNextTab(int direction)
        {
            if (_tabManager != null)
            {
                await _tabManager.ActivateNextAsync(direction);
            }
        }

        private async void ActivateTabByIndex(int index)
        {
            if (_tabManager != null)
            {
                await _tabManager.ActivateIndexAsync(index);
            }
        }

        private async void ActivateLastTab()
        {
            if (_tabManager != null && _tabManager.Tabs.Count > 0)
            {
                await _tabManager.ActivateIndexAsync(_tabManager.Tabs.Count - 1);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (TryHandleShortcut(keyData))
            {
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// WebView2 是原生子窗口，页面获得焦点时 WinForms 的 ProcessCmdKey 收不到按键，
        /// 用消息过滤器在本线程内兜底。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            // WM_KEYDOWN / WM_SYSKEYDOWN
            if (m.Msg != 0x0100 && m.Msg != 0x0104)
            {
                return false;
            }

            if (Form.ActiveForm != this)
            {
                return false;
            }

            Keys key = (Keys)(int)m.WParam & Keys.KeyCode;
            Keys modifiers = Control.ModifierKeys & (Keys.Control | Keys.Shift | Keys.Alt);
            return TryHandleShortcut(key | modifiers);
        }

        private bool TryHandleShortcut(Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.D))
            {
                ToggleBookmark();
                return true;
            }

            if (keyData == (Keys.Control | Keys.H))
            {
                ShowInternalPage("about:history");
                return true;
            }

            // Ctrl+Shift+O 被 AMD Software 占用，主快捷键改为 Ctrl+Alt+B，两者都保留。
            if (keyData == (Keys.Control | Keys.Alt | Keys.B) ||
                keyData == (Keys.Control | Keys.Shift | Keys.O))
            {
                ShowBookmarkManager();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Delete))
            {
                ClearHistory();
                return true;
            }

            if (keyData == (Keys.Control | Keys.L))
            {
                _navigationBar.FocusAddressBar();
                return true;
            }

            if (keyData == (Keys.Control | Keys.T))
            {
                CreateNewTab();
                return true;
            }

            if (keyData == (Keys.Control | Keys.W))
            {
                CloseActiveTab();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Tab))
            {
                ActivateNextTab(1);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                ActivateNextTab(-1);
                return true;
            }

            if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R))
            {
                Reload();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Left))
            {
                GoBack();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Right))
            {
                GoForward();
                return true;
            }

            if ((keyData & Keys.Modifiers) == Keys.Control)
            {
                Keys code = keyData & Keys.KeyCode;

                if (code >= Keys.D1 && code <= Keys.D8)
                {
                    ActivateTabByIndex(code - Keys.D1);
                    return true;
                }

                if (code == Keys.D9)
                {
                    ActivateLastTab();
                    return true;
                }
            }

            return false;
        }
    }
}
