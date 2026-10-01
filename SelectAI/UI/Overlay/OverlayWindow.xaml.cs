using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
using SelectAI.SmartActions;
using Point = System.Windows.Point;
using SelectionMode = SelectAI.Core.Enums.SelectionMode;

namespace SelectAI.UI.Overlay;

public partial class OverlayWindow : Window
{
    private readonly IScreenCapture _screenCapture;
    private readonly IOcrProvider _ocrProvider;
    private readonly ISearchProvider _searchProvider;
    private readonly AiProviderFactory _aiProviderFactory;
    private readonly ISettingsService _settingsService;

    private CapturedScreen? _currentScreen;
    private SelectionRegion? _currentSelection;
    private OcrResult? _currentOcrResult;

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

        // Wire toolbar events
        Toolbar.AskAiRequested += OnToolbarAskAi;
        Toolbar.GoogleSearchRequested += OnToolbarGoogleSearch;
        Toolbar.GoogleLensRequested += OnToolbarGoogleLens;
        Toolbar.ExtractTextRequested += OnToolbarExtractText;
        Toolbar.TranslateRequested += OnToolbarTranslate;
        Toolbar.CopyRequested += OnToolbarCopy;
        Toolbar.SaveRequested += OnToolbarSave;
        Toolbar.CloseRequested += (_, _) => CloseOverlay();
        Toolbar.SmartActionRequested += OnToolbarSmartAction;

        // Wire AI panel events
        AiPanel.CloseRequested += (_, _) => AiPanel.Visibility = Visibility.Collapsed;

        // Wire selection canvas
        OverlayCanvas.SelectionCompleted += OnSelectionFinished;
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

            // 3. Reset UI states
            OverlayCanvas.ResetSelection();
            Toolbar.Visibility = Visibility.Collapsed;
            AiPanel.Visibility = Visibility.Collapsed;
            ToastNotification.Visibility = Visibility.Collapsed;
            ModeBar.Visibility = Visibility.Visible;

            Topmost = true;
            Show();
            Activate();
            Focus();

            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                    NativeMethods.SetForegroundWindow(handle);
                    NativeMethods.BringWindowToTop(handle);
                }
            }
            catch { }

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

    private void SetMode(SelectAI.Core.Enums.SelectionMode mode)
    {
        OverlayCanvas.Mode = mode;
        RbFreeform.IsChecked = (mode == SelectAI.Core.Enums.SelectionMode.Freeform);
        RbRectangle.IsChecked = (mode == SelectAI.Core.Enums.SelectionMode.Rectangle);
        RbText.IsChecked = (mode == SelectAI.Core.Enums.SelectionMode.Text);
        RbImage.IsChecked = (mode == SelectAI.Core.Enums.SelectionMode.Image);
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (RbFreeform.IsChecked == true) OverlayCanvas.Mode = SelectAI.Core.Enums.SelectionMode.Freeform;
        else if (RbRectangle.IsChecked == true) OverlayCanvas.Mode = SelectAI.Core.Enums.SelectionMode.Rectangle;
        else if (RbText.IsChecked == true) OverlayCanvas.Mode = SelectAI.Core.Enums.SelectionMode.Text;
        else if (RbImage.IsChecked == true) OverlayCanvas.Mode = SelectAI.Core.Enums.SelectionMode.Image;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Toolbar.Visibility = Visibility.Collapsed;
            AiPanel.Visibility = Visibility.Collapsed;
            OverlayCanvas.HandleMouseDown(e.GetPosition(OverlayCanvas));
        }
    }

    private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        OverlayCanvas.HandleMouseMove(e.GetPosition(OverlayCanvas));
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        OverlayCanvas.HandleMouseUp(e.GetPosition(OverlayCanvas));
    }

    private async void OnSelectionFinished(object? sender, List<PointF> points)
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

        // Crop image from frozen full bitmap
        Bitmap cropped;
        if (OverlayCanvas.Mode == SelectionMode.Freeform)
        {
            cropped = _screenCapture.CropFreeform(_currentScreen.FullBitmap, physicalPoints, boundingBoxF);
        }
        else
        {
            cropped = _screenCapture.CropRegion(_currentScreen.FullBitmap, boundingBoxF);
        }

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

        // Reposition toolbar near selection
        PositionToolbar(box);

        // Run OCR asynchronously
        _currentOcrResult = await _ocrProvider.RecognizeTextAsync(cropped);

        if (_currentOcrResult != null && _currentOcrResult.HasText)
        {
            var entities = SmartContentDetector.DetectEntities(_currentOcrResult.FullText);
            Toolbar.SetDetectedEntity(entities.FirstOrDefault());

            if (_settingsService.CurrentSettings.CopyOcrAutomatically)
            {
                System.Windows.Clipboard.SetText(_currentOcrResult.FullText);
                ShowToast("OCR text copied to clipboard!");
            }
        }
        else
        {
            Toolbar.SetDetectedEntity(null);
        }

        Toolbar.Visibility = Visibility.Visible;
    }

    private void PositionToolbar(Rect selectionBounds)
    {
        Toolbar.UpdateLayout();
        double tbWidth = Toolbar.ActualWidth > 0 ? Toolbar.ActualWidth : 480;
        double tbHeight = Toolbar.ActualHeight > 0 ? Toolbar.ActualHeight : 48;

        // Horizontally center relative to selection
        double x = selectionBounds.Left + (selectionBounds.Width - tbWidth) / 2.0;

        // Clamp to virtual screen edges
        double minX = 16;
        double maxX = ActualWidth - tbWidth - 16;
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

    private void OnToolbarAskAi(object? sender, EventArgs e)
    {
        if (_currentSelection == null) return;

        var activeProvider = _aiProviderFactory.GetCurrentProvider();
        AiPanel.Setup(activeProvider, _currentSelection, _currentOcrResult?.FullText);

        // Position AI panel beside or near toolbar
        double tbX = Canvas.GetLeft(Toolbar);
        double tbY = Canvas.GetTop(Toolbar);

        double aiX = Math.Max(16, Math.Min(ActualWidth - 440, tbX));
        double aiY = tbY + 54;
        if (aiY + 380 > ActualHeight)
        {
            aiY = Math.Max(16, tbY - 380);
        }

        Canvas.SetLeft(AiPanel, aiX);
        Canvas.SetTop(AiPanel, aiY);
        AiPanel.Visibility = Visibility.Visible;
    }

    private void OnToolbarGoogleSearch(object? sender, EventArgs e)
    {
        if (_currentOcrResult != null && _currentOcrResult.HasText)
        {
            _searchProvider.SearchText(_currentOcrResult.FullText);
            CloseOverlay();
        }
        else if (_currentSelection?.CroppedBitmap != null)
        {
            var bytes = ImageHelper.ToPngBytes(_currentSelection.CroppedBitmap);
            _searchProvider.SearchImage(bytes);
            CloseOverlay();
        }
        else
        {
            _searchProvider.SearchText("");
            CloseOverlay();
        }
    }

    private void OnToolbarGoogleLens(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedBitmap != null)
        {
            var bytes = ImageHelper.ToPngBytes(_currentSelection.CroppedBitmap);
            _searchProvider.SearchImage(bytes);
            CloseOverlay();
        }
    }

    private void OnToolbarExtractText(object? sender, EventArgs e)
    {
        if (_currentOcrResult != null && _currentOcrResult.HasText)
        {
            System.Windows.Clipboard.SetText(_currentOcrResult.FullText);
            ShowToast("Text extracted & copied to clipboard!");
        }
        else
        {
            ShowToast("No text recognized in this area.");
        }
    }

    private void OnToolbarTranslate(object? sender, EventArgs e)
    {
        if (_currentOcrResult != null && _currentOcrResult.HasText)
        {
            var encoded = Uri.EscapeDataString(_currentOcrResult.FullText);
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://translate.google.com/?sl=auto&tl=en&text={encoded}&op=translate",
                UseShellExecute = true
            });
            CloseOverlay();
        }
        else
        {
            ShowToast("No text available to translate.");
        }
    }

    private void OnToolbarCopy(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedImageSource != null)
        {
            System.Windows.Clipboard.SetImage(_currentSelection.CroppedImageSource);
            ShowToast("Image copied to clipboard!");
        }
    }

    private void OnToolbarSave(object? sender, EventArgs e)
    {
        if (_currentSelection?.CroppedBitmap == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
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

    private void OnToolbarSmartAction(object? sender, DetectedEntity entity)
    {
        switch (entity.Type)
        {
            case ContentType.Url:
                Process.Start(new ProcessStartInfo { FileName = entity.Value, UseShellExecute = true });
                CloseOverlay();
                break;
            case ContentType.Email:
                Process.Start(new ProcessStartInfo { FileName = $"mailto:{entity.Value}", UseShellExecute = true });
                CloseOverlay();
                break;
            case ContentType.PhoneNumber:
                System.Windows.Clipboard.SetText(entity.Value);
                ShowToast("Phone copied to clipboard!");
                break;
            case ContentType.Code:
                OnToolbarAskAi(this, EventArgs.Empty);
                _ = AiPanel.AskQuestionAsync("Explain this code, point out any bugs or issues, and provide suggested improvements.");
                break;
            case ContentType.SearchQuery:
                _searchProvider.SearchText(entity.Value);
                CloseOverlay();
                break;
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

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
        Hide();
        OverlayCanvas.ResetSelection();
        Toolbar.Visibility = Visibility.Collapsed;
        AiPanel.Visibility = Visibility.Collapsed;
        ToastNotification.Visibility = Visibility.Collapsed;
        _currentScreen?.Dispose();
        _currentScreen = null;
        _currentSelection?.Dispose();
        _currentSelection = null;
    }
}
