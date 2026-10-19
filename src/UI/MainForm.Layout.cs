using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace VantageLite.UI
{
    /// <summary>
    /// MainForm 的界面搭建部分：只放控件创建、布局、事件接线。
    /// 依赖：NavigationBar / WebView2。改动注意：业务逻辑留在 MainForm.cs。
    /// </summary>
    public sealed partial class MainForm
    {
        private void BuildLayout()
        {
            SuspendLayout();

            Text = "Vantage Lite";
            Icon = LoadApplicationIcon();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 800);
            MinimumSize = new Size(800, 560);
            BackColor = Color.FromArgb(24, 27, 33);

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = Color.White;

            _navigationBar = new NavigationBar();
            _navigationBar.Dock = DockStyle.Top;

            Controls.Add(_webView);
            Controls.Add(_navigationBar);

            ResumeLayout(true);
        }

        private void WireNavigationEvents()
        {
            _navigationBar.NavigateRequested += delegate (object sender, string address)
            {
                NavigateTo(address);
            };
            _navigationBar.BackRequested += delegate { GoBack(); };
            _navigationBar.ForwardRequested += delegate { GoForward(); };
            _navigationBar.ReloadRequested += delegate { Reload(); };
            _navigationBar.StopRequested += delegate { StopLoading(); };
            _navigationBar.HomeRequested += delegate { NavigateTo(DefaultHomeUrl); };
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }
}
