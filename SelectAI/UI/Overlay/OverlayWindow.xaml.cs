using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using SelectAI.AI;
using SelectAI.Capture;
using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;
using SelectAI.Core.Utils;
using SelectAI.Ocr;
using SelectAI.Search;
using SelectAI.Settings;
using Point = System.Windows.Point;
using SelectionMode = SelectAI.Core.Enums.SelectionMode;

namespace SelectAI.UI.Overlay;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly IScreenCapture _screenCapture;
    private readonly IOcrProvider _ocrProvider;
    private readonly ISearchProvider _searchProvider;
    private readonly AiProviderFactory _aiProviderFactory;
    private readonly ISettingsService _settingsService;

    private CapturedScreen? _currentScreen;
    private SelectionRegion? _currentSelection;

    public OverlayWindow(
        IScreenCapture screenCapture,
        IOcrProvider ocrProvider,
        ISearchProvider searchProvider,
        AiProviderFactory aiProviderFactory,
        ISettingsService settingsService)
    {
        InitializeComponent();

        _screenCapture = screenCapture;
        _ocrProvider = ocrProvider;
        _searchProvider = searchProvider;
        _aiProviderFactory = aiProviderFactory;
        _settingsService = settingsService;

        // Wire floating pill toolbar events (Copy, Share, Save)
        Toolbar.CopyRequested += OnToolbarCopy;
        Toolbar.ShareRequested += OnToolbarShare;
        Toolbar.SaveRequested += OnToolbarSave;

        // Wire side search panel events
        SearchSidePanel.CloseRequested += (_, _) => CloseOverlay();
        SearchSidePanel.ShowInBrowserRequested += OnShowInBrowser;
        SearchSidePanel.EngineChanged += OnEngineChanged;

        // Wire selection canvas
        OverlayCanvas.SelectionCompleted += OnSelectionFinished;
        OverlayCanvas.BoundingBoxChanged += (s, box) => PositionToolbar(box);

        // Wire window deactivation for fluid app switching and Win+D
        Deactivated += OnWindowDeactivated;
    }

    private void OnEngineChanged(object? sender, string engine)
    {
        if (_currentSelection?.CroppedBitmap == null) return;

        var bytes = ImageHelper.ToPngBytes(_currentSelection.CroppedBitmap);
        _ = Task.Run(async () =>
        {
            string url;
            if (engine == "bing")
            {
                url = "https://www.bing.com/visualsearch";
            }
            else
            {
                url = await GoogleLensService.UploadImageAsync(bytes);
            }
            await Dispatcher.InvokeAsync(() => SearchSidePanel.NavigateToUrlAsync(url));
        });
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        try
        {
            var myHwnd = new WindowInteropHelper(this).Handle;
            var fgHwnd = GetForegroundWindow();

            // If focus moved to desktop or another application (Win+D, Alt+Tab, Taskbar click)
            if (fgHwnd != IntPtr.Zero && fgHwnd != myHwnd && !IsChild(myHwnd, fgHwnd))
            {
                CloseOverlay();
            }
        }
        catch
        {
            CloseOverlay();
        }
    }

    public void StartSelection()
    {
        try
        {
            AppLog.Info("OverlayWindow.StartSelection() triggered.");

            // 1. Capture Virtual Desktop across all monitors
            _currentScreen?.Dispose();
            _currentScreen = _screenCapture.CaptureVirtualDesktop();

            // Position window across all monitors using WPF DIPs
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            if (_currentScreen?.CachedWpfBitmap != null)
            {
                ImgFrozenBackground.Source = _currentScreen.CachedWpfBitmap;
            }

            // 2. Set default mode from settings
            var defaultMode = _settingsService.CurrentSettings.DefaultMode;
            SetMode(defaultMode);

            // 3. Reset UI states & transforms
            ResetTransforms();
            OverlayCanvas.ResetSelection();
            Toolbar.Visibility = Visibility.Collapsed;
            ToastNotification.Visibility = Visibility.Collapsed;
            ModeBar.Visibility = Visibility.Visible;

            Topmost = true;
            Show();
            Activate();
            Focus();

            AppLog.Info("OverlayWindow shown and focused successfully.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to start selection", ex);
            try
            {
                Show();
                Activate();
                ShowToast("Selection mode active (fallback mode)");
            }
            catch
            {
                CloseOverlay();
            }
        }
    }

    private void SetMode(SelectionMode mode)
    {
        OverlayCanvas.Mode = mode;
        RbFreeform.IsChecked = (mode == SelectionMode.Freeform);
        RbRectangle.IsChecked = (mode == SelectionMode.Rectangle);
        RbText.IsChecked = (mode == SelectionMode.Text);
        RbImage.IsChecked = (mode == SelectionMode.Image);
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (RbFreeform.IsChecked == true) OverlayCanvas.Mode = SelectionMode.Freeform;
        else if (RbRectangle.IsChecked == true) OverlayCanvas.Mode = SelectionMode.Rectangle;
        else if (RbText.IsChecked == true) OverlayCanvas.Mode = SelectionMode.Text;
        else if (RbImage.IsChecked == true) OverlayCanvas.Mode = SelectionMode.Image;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Toolbar.Visibility = Visibility.Collapsed;
            OverlayCanvas.HandleMouseDown(e.GetPosition(OverlayCanvas));
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        OverlayCanvas.HandleMouseMove(e.GetPosition(OverlayCanvas));
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        OverlayCanvas.HandleMouseUp(e.GetPosition(OverlayCanvas));
    }

    private void OnSelectionFinished(object? sender, List<PointF> points)
    {
        if (_currentScreen == null) return;

        var box = OverlayCanvas.CurrentBoundingBox;
        double scaleX = (double)_currentScreen.FullBitmap.Width / Math.Max(1.0, ActualWidth);
        double scaleY = (double)_currentScreen.FullBitmap.Height / Math.Max(1.0, ActualHeight);

        var boundingBoxF = new RectangleF(
            (float)(box.X * scaleX),
            (float)(box.Y * scaleY),
            (float)(box.Width * scaleX),
            (float)(box.Height * scaleY));

        var physicalPoints = points.Select(p => new PointF((float)(p.X * scaleX), (float)(p.Y * scaleY))).ToList();

        // Crop clean intact rectangular region (Samsung Galaxy AI / Circle to Search standard)
        Bitmap cropped = _screenCapture.CropRegion(_currentScreen.FullBitmap, boundingBoxF);

        _currentSelection?.Dispose();
        _currentSelection = new SelectionRegion
        {
            Mode = OverlayCanvas.Mode,
            RawPoints = points,
            SmoothedPoints = points,
            BoundingBox = boundingBoxF,
            CroppedBitmap = cropped,
            CroppedImageSource = ImageHelper.ToBitmapSource(cropped)
        };

        // Position pill toolbar right below the selection box
        PositionToolbar(box);
        Toolbar.Visibility = Visibility.Visible;

        // Display thumbnail in side panel search capsule matching Image 2
        SearchSidePanel.SetThumbnail(_currentSelection?.CroppedImageSource);

        // Animate desktop shift left and open side panel (Samsung Galaxy AI split-screen)
        OpenSidePanel();

        // Upload to Google Lens and navigate WebView in side panel
        var bytes = ImageHelper.ToPngBytes(cropped);
        _ = Task.Run(async () =>
        {
            var lensUrl = await GoogleLensService.UploadImageAsync(bytes);
            await Dispatcher.InvokeAsync(() => SearchSidePanel.NavigateToUrlAsync(lensUrl));
        });
    }

    private void PositionToolbar(Rect selectionBounds)
    {
        Toolbar.UpdateLayout();
        double tbWidth = 240;
        double tbHeight = 42;

        // Horizontally center relative to selection
        double x = selectionBounds.Left + (selectionBounds.Width - tbWidth) / 2.0;

        // Clamp to virtual screen edges
        double minX = 16;
        double maxX = Math.Max(minX, ActualWidth - 440 - tbWidth - 16);
        x = Math.Max(minX, Math.Min(maxX, x));

        // Vertically place below selection if space permits, otherwise above
        double y;
        if (selectionBounds.Bottom + tbHeight + 16 <= ActualHeight)
        {
            y = selectionBounds.Bottom + 12;
        }
        else
        {
            y = Math.Max(16, selectionBounds.Top - tbHeight - 12);
        }

        Canvas.SetLeft(Toolbar, x);
        Canvas.SetTop(Toolbar, y);
    }

    private void OpenSidePanel()
    {
        SidePanelContainer.Visibility = Visibility.Visible;

        // Yield modal topmost lock so other apps or taskbar clicks can activate seamlessly
        Topmost = false;

        // Slide in from right (Galaxy AI style)
        var panelAnim = new DoubleAnimation
        {
            From = 450,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SidePanelTranslateTransform.BeginAnimation(TranslateTransform.XProperty, panelAnim);

        // Shift frozen desktop slightly left
        var shiftAnim = new DoubleAnimation
        {
            From = 0,
            To = -120,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DesktopShiftTransform.BeginAnimation(TranslateTransform.XProperty, shiftAnim);
    }

    private void ResetTransforms()
    {
        DesktopShiftTransform.BeginAnimation(TranslateTransform.XProperty, null);
        DesktopShiftTransform.X = 0;
        SidePanelTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        SidePanelTranslateTransform.X = 450;
        SidePanelContainer.Visibility = Visibility.Collapsed;
    }

    private void OnShowInBrowser(object? sender, string url)
    {
        BrowserHelper.OpenUrl(url, SearchSidePanel.SelectedBrowserId);
        CloseOverlay();
    }

    private void OnToolbarCopy(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedImageSource != null)
        {
            Clipboard.SetImage(_currentSelection.CroppedImageSource);
            ShowToast("Image copied to clipboard!");
        }
    }

    private void OnToolbarShare(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedImageSource != null)
        {
            Clipboard.SetImage(_currentSelection.CroppedImageSource);
            ShowToast("Image copied to clipboard for sharing!");
        }
    }

    private void OnToolbarSave(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedBitmap == null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
            FileName = $"SelectAI_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _currentSelection.CroppedBitmap.Save(dlg.FileName);
                ShowToast("Image saved successfully!");
            }
            catch (Exception ex)
            {
                ShowToast($"Save failed: {ex.Message}");
            }
        }
    }

    private void ShowToast(string message)
    {
        ToastMessage.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (s, e) =>
        {
            ToastNotification.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOverlay();
            e.Handled = true;
        }
        else if (e.Key == Key.D1)
        {
            SetMode(SelectionMode.Freeform);
            e.Handled = true;
        }
        else if (e.Key == Key.D2)
        {
            SetMode(SelectionMode.Rectangle);
            e.Handled = true;
        }
        else if (e.Key == Key.D3)
        {
            SetMode(SelectionMode.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.D4)
        {
            SetMode(SelectionMode.Image);
            e.Handled = true;
        }
    }

    public void CloseOverlay()
    {
        ResetTransforms();
        Hide();
        OverlayCanvas.ResetSelection();
        Toolbar.Visibility = Visibility.Collapsed;
        ToastNotification.Visibility = Visibility.Collapsed;
        _currentScreen?.Dispose();
        _currentScreen = null;
        _currentSelection?.Dispose();
        _currentSelection = null;
    }
}
