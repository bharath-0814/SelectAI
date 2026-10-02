using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SelectAI.Core.Utils;

namespace SelectAI.UI.Overlay;

public partial class SideSearchPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ShowInBrowserRequested;
    public event EventHandler<string>? EngineChanged;

    private string _currentUrl = "https://lens.google.com/";
    private string _selectedBrowserId = "chrome";
    private string _selectedEngine = "google";
    private bool _isWebViewInitialized = false;

    public SideSearchPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_isWebViewInitialized) return;

        try
        {
            var tempUserData = Path.Combine(Path.GetTempPath(), "SelectAI_WebViewProfile");
            // Force Dark Theme in WebView2 matching Galaxy AI dark mode
            var options = new CoreWebView2EnvironmentOptions("--enable-features=WebContentsForceDark --force-dark-mode");
            var env = await CoreWebView2Environment.CreateAsync(null, tempUserData, options);
            await WebViewControl.EnsureCoreWebView2Async(env);

            // Responsive User-Agent prevents horizontal scrollbars and forces clean single-column visual search
            WebViewControl.CoreWebView2.Settings.UserAgent = 
                "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.6723.102 Mobile Safari/537.36";

            WebViewControl.NavigationStarting += (_, _) =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            };

            WebViewControl.NavigationCompleted += (_, args) =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                if (args.IsSuccess && WebViewControl.Source != null)
                {
                    _currentUrl = WebViewControl.Source.ToString();
                    var q = GetDisplayQuery(_currentUrl);
                    if (!string.IsNullOrEmpty(q))
                    {
                        TxtSearchQuery.Text = q;
                    }
                }
            };

            _isWebViewInitialized = true;

            if (!string.IsNullOrEmpty(_currentUrl))
            {
                WebViewControl.CoreWebView2.Navigate(_currentUrl);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to initialize WebView2 in SideSearchPanel", ex);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    public async Task NavigateToUrlAsync(string url)
    {
        _currentUrl = url;
        var q = GetDisplayQuery(url);
        if (!string.IsNullOrEmpty(q))
        {
            TxtSearchQuery.Text = q;
        }

        LoadingOverlay.Visibility = Visibility.Visible;

        await InitializeWebViewAsync();

        try
        {
            if (WebViewControl.CoreWebView2 != null)
            {
                WebViewControl.CoreWebView2.Navigate(url);
            }
            else
            {
                WebViewControl.Source = new Uri(url);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed navigating WebView2 to {url}", ex);
        }
    }

    public void SetThumbnail(ImageSource? imageSource)
    {
        ImgSelectionThumbnail.Source = imageSource;
    }

    public string CurrentUrl => _currentUrl;
    public string SelectedBrowserId => _selectedBrowserId;
    public string SelectedEngine => _selectedEngine;

    private string GetDisplayQuery(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var q = query["q"] ?? query["text"] ?? query["p"];
            if (!string.IsNullOrEmpty(q)) return q;
        }
        catch { }
        return "";
    }

    private void OnBrowserSelectorClick(object sender, RoutedEventArgs e)
    {
        // Open the dropdown menu directly beneath the button
        BrowserContextMenu.PlacementTarget = BtnBrowserSelector;
        BrowserContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        BrowserContextMenu.IsOpen = true;
    }

    private void OnSelectGoogle(object sender, RoutedEventArgs e)
    {
        _selectedEngine = "google";
        _selectedBrowserId = "chrome";
        TxtEngineIcon.Text = "🌐";
        TxtEngineName.Text = "Google";
        EngineChanged?.Invoke(this, "google");
    }

    private void OnSelectBing(object sender, RoutedEventArgs e)
    {
        _selectedEngine = "bing";
        _selectedBrowserId = "edge";
        TxtEngineIcon.Text = "🔷";
        TxtEngineName.Text = "Bing";
        EngineChanged?.Invoke(this, "bing");
    }

    private void OnSelectBrave(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "brave";
        TxtEngineIcon.Text = "🦁";
        TxtEngineName.Text = "Brave";
    }

    private void OnSelectFirefox(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "firefox";
        TxtEngineIcon.Text = "🦊";
        TxtEngineName.Text = "Firefox";
    }

    private void OnSelectEdge(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "edge";
        TxtEngineIcon.Text = "🌊";
        TxtEngineName.Text = "Edge";
    }

    private void OnAddCustomBrowser(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Browser Executable",
            Filter = "Applications (*.exe)|*.exe"
        };

        if (dlg.ShowDialog() == true)
        {
            var path = dlg.FileName;
            var name = Path.GetFileNameWithoutExtension(path);
            var id = "custom_" + Guid.NewGuid().ToString("N")[..4];

            BrowserHelper.AddCustomBrowser(name, path);
            _selectedBrowserId = id;
            TxtEngineIcon.Text = "🚀";
            TxtEngineName.Text = name;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (WebViewControl.CanGoBack)
        {
            WebViewControl.GoBack();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnShowInBrowserClick(object sender, RoutedEventArgs e)
    {
        ShowInBrowserRequested?.Invoke(this, _currentUrl);
    }

    private void OnSearchQueryClick(object sender, RoutedEventArgs e)
    {
        ExecuteSearch();
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSearch();
            e.Handled = true;
        }
    }

    private void ExecuteSearch()
    {
        var text = TxtSearchQuery.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string searchUrl = _selectedEngine == "bing"
            ? $"https://www.bing.com/search?q={Uri.EscapeDataString(text)}"
            : $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";

        _ = NavigateToUrlAsync(searchUrl);
    }
}
