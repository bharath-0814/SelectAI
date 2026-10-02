using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SelectAI.Core.Utils;

namespace SelectAI.UI.Overlay;

public partial class SideSearchPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ShowInBrowserRequested;

    private string _currentUrl = "https://lens.google.com/";
    private string _selectedBrowserId = "chrome";
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
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: tempUserData);
            await WebViewControl.EnsureCoreWebView2Async(env);

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
                    TxtSearchQuery.Text = GetDisplayQuery(_currentUrl);
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
        TxtSearchQuery.Text = GetDisplayQuery(url);
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

    public string CurrentUrl => _currentUrl;

    public string SelectedBrowserId => _selectedBrowserId;

    private string GetDisplayQuery(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var q = query["q"];
            if (!string.IsNullOrEmpty(q)) return q;
        }
        catch { }
        return "";
    }

    private void OnBrowserChanged(object sender, RoutedEventArgs e)
    {
        if (RbChrome.IsChecked == true) _selectedBrowserId = "chrome";
        else if (RbBrave.IsChecked == true) _selectedBrowserId = "brave";
        else if (RbFirefox.IsChecked == true) _selectedBrowserId = "firefox";
        else if (RbEdge.IsChecked == true) _selectedBrowserId = "edge";
    }

    private void OnAddBrowserClick(object sender, RoutedEventArgs e)
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

            var rb = new RadioButton
            {
                Content = name,
                Style = (Style)FindResource("BrowserTabButton"),
                IsChecked = true
            };
            rb.Checked += (s, ev) => _selectedBrowserId = id;
            _selectedBrowserId = id;

            // Insert before the "+ Add More" button
            int count = BrowserPillsContainer.Children.Count;
            BrowserPillsContainer.Children.Insert(Math.Max(1, count - 1), rb);
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

        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";
        _ = NavigateToUrlAsync(searchUrl);
    }
}
