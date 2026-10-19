using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VantageLite.App;
using VantageLite.Browser;
using VantageLite.Diagnostics;

namespace VantageLite.UI
{
    /// <summary>
    /// 主窗口：装配 UI、初始化 WebView2、处理导航事件与快捷键。
    /// 依赖：Browser / Diagnostics / App。改动注意：布局在 MainForm.Layout.cs。
    /// </summary>
    public sealed partial class MainForm : Form
    {
        private const string DefaultHomeUrl = "https://asystech.cn/start/index.html";

        private readonly CommandLineOptions _options;
        private NavigationBar _navigationBar;
        private WebView2 _webView;
        private CoreWebView2 _core;
        private bool _isLoading;

        public MainForm()
            : this(new CommandLineOptions())
        {
        }

        public MainForm(CommandLineOptions options)
        {
            _options = options ?? new CommandLineOptions();
            BuildLayout();
            WireNavigationEvents();

            Load += MainForm_Load;
            FormClosed += MainForm_FormClosed;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                Log.Info("Creating WebView2 environment...");
                CoreWebView2Environment environment = await WebViewEnvironment.CreateAsync(_options);

                await _webView.EnsureCoreWebView2Async(environment);
                _core = _webView.CoreWebView2;
                ConfigureBrowser();

                Log.Info("WebView2 initialized.");
                NavigateTo(_options.StartUrl ?? DefaultHomeUrl);
            }
            catch (Exception ex)
            {
                Log.Error("WebView2 初始化失败", ex);
                MessageBox.Show(
                    this,
                    "浏览器内核初始化失败：\n" + ex.Message,
                    "Vantage Lite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
            }
        }

        private void ConfigureBrowser()
        {
            _core.Settings.AreDevToolsEnabled = _options.EnableDevTools;
            _core.Settings.AreDefaultContextMenusEnabled = true;
            _core.Settings.IsStatusBarEnabled = true;
            _core.Settings.IsSwipeNavigationEnabled = false;

            _core.SourceChanged += Core_SourceChanged;
            _core.DocumentTitleChanged += Core_DocumentTitleChanged;
            _core.HistoryChanged += Core_HistoryChanged;
            _core.NavigationStarting += Core_NavigationStarting;
            _core.NavigationCompleted += Core_NavigationCompleted;
            _core.NewWindowRequested += Core_NewWindowRequested;
            _core.ProcessFailed += Core_ProcessFailed;
        }

        private void NavigateTo(string address)
        {
            if (_core == null)
            {
                Log.Warn("Navigate ignored before WebView2 initialization: " + address);
                return;
            }

            string url = UrlHelper.Normalize(address);
            if (url == null)
            {
                return;
            }

            Log.Info("Navigate: " + url);
            _core.Navigate(url);
        }

        private void GoBack()
        {
            if (_core != null && _core.CanGoBack)
            {
                _core.GoBack();
            }
        }

        private void GoForward()
        {
            if (_core != null && _core.CanGoForward)
            {
                _core.GoForward();
            }
        }

        private void Reload()
        {
            if (_core != null)
            {
                _core.Reload();
            }
        }

        private void StopLoading()
        {
            if (_core != null)
            {
                _core.Stop();
            }
        }

        private void UpdateNavigationState()
        {
            if (_core == null)
            {
                return;
            }

            _navigationBar.SetNavigationState(_core.CanGoBack, _core.CanGoForward);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.L))
            {
                _navigationBar.FocusAddressBar();
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

            if (keyData == (Keys.Control | Keys.W))
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Log.Info("Main window closed.");
            if (_webView != null)
            {
                _webView.Dispose();
            }
        }
    }
}
