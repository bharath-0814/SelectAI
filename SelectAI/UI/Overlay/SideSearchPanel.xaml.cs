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

    private string _currentUrl = "https://www.bing.com/visualsearch";
    private string _selectedBrowserId = "edge";
    private string _selectedEngine = "bing";
    private bool _isWebViewInitialized = false;
    private TaskCompletionSource<bool>? _initTcs;

    public SideSearchPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isWebViewInitialized) return;

        if (_initTcs != null)
        {
            await _initTcs.Task;
            return;
        }

        _initTcs = new TaskCompletionSource<bool>();

        try
        {
            var tempUserData = Path.Combine(Path.GetTempPath(), "SelectAI_WebViewProfile");
            var env = await CoreWebView2Environment.CreateAsync(null, tempUserData);
            await WebViewControl.EnsureCoreWebView2Async(env);

            // Desktop Chrome User-Agent ensures full search & AI overview script execution
            WebViewControl.CoreWebView2.Settings.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

            WebViewControl.NavigationStarting += (_, _) =>
            {
                Dispatcher.Invoke(() => LoadingOverlay.Visibility = Visibility.Visible);
            };

            WebViewControl.NavigationCompleted += (_, args) =>
            {
                Dispatcher.Invoke(() =>
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
                });
            };

            _isWebViewInitialized = true;
            _initTcs.SetResult(true);

            if (!string.IsNullOrEmpty(_currentUrl))
            {
                WebViewControl.CoreWebView2.Navigate(_currentUrl);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to initialize WebView2 in SideSearchPanel", ex);
            _initTcs.SetException(ex);
            Dispatcher.Invoke(() => LoadingOverlay.Visibility = Visibility.Collapsed);
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

        await EnsureInitializedAsync();

        try
        {
            if (WebViewControl.CoreWebView2 != null)
            {
                WebViewControl.CoreWebView2.Navigate(url);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed navigating WebView2 to {url}", ex);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    public void SetThumbnail(ImageSource? imageSource)
    {
        ImgSelectionThumbnail.Source = imageSource;
    }

    public void SetQueryText(string text)
    {
        TxtSearchQuery.Text = text;
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

    private void OnSelectBing(object sender, RoutedEventArgs e)
    {
        _selectedEngine = "bing";
        _selectedBrowserId = "edge";
        TxtEngineIcon.Text = "🔷";
        TxtEngineName.Text = "Bing";
        EngineChanged?.Invoke(this, "bing");
    }

    private void OnSelectGoogle(object sender, RoutedEventArgs e)
    {
        _selectedEngine = "google";
        _selectedBrowserId = "chrome";
        TxtEngineIcon.Text = "🌐";
        TxtEngineName.Text = "Google";
        EngineChanged?.Invoke(this, "google");
    }

    private void OnSelectBrave(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "brave";
        TxtEngineIcon.Text = "🦁";
        TxtEngineName.Text = "Brave";
        EngineChanged?.Invoke(this, _selectedEngine);
    }

    private void OnSelectFirefox(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "firefox";
        TxtEngineIcon.Text = "🦊";
        TxtEngineName.Text = "Firefox";
        EngineChanged?.Invoke(this, _selectedEngine);
    }

    private void OnSelectEdge(object sender, RoutedEventArgs e)
    {
        _selectedBrowserId = "edge";
        TxtEngineIcon.Text = "🌊";
        TxtEngineName.Text = "Edge";
        EngineChanged?.Invoke(this, _selectedEngine);
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
        PerformManualQuerySearch();
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformManualQuerySearch();
        }
    }

    private void PerformManualQuerySearch()
    {
        var query = TxtSearchQuery.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;

        string targetUrl = _selectedEngine == "bing"
            ? $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}"
            : $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";

        _ = NavigateToUrlAsync(targetUrl);
    }
}
