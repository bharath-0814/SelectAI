using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Utils;
using SelectAI.Hotkeys;

namespace SelectAI.UI.Main;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly GlobalHotkeyManager? _hotkeyManager;
    private readonly Action _startSelectionAction;
    private readonly Action _openSettingsAction;
    private bool _isExplicitExit;

    public MainWindow(
        ISettingsService settingsService,
        GlobalHotkeyManager? hotkeyManager,
        Action startSelectionAction,
        Action openSettingsAction)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;
        _startSelectionAction = startSelectionAction;
        _openSettingsAction = openSettingsAction;

        Loaded += (s, e) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);
        };

        PreviewKeyDown += OnPreviewKeyDown;
        LoadState();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (App.WmShowAppMessage != 0 && (uint)msg == App.WmShowAppMessage)
        {
            AppLog.Info("MainWindow received WmShowAppMessage -> Restoring window.");
            WindowState = WindowState.Normal;
            Show();
            Activate();
            Focus();
            NativeMethods.SetForegroundWindow(hwnd);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool hasCtrlShift = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift);
        if (hasCtrlShift && (e.Key == Key.C || e.Key == Key.S || e.Key == Key.Space))
        {
            e.Handled = true;
            OnStartSelectionClick(sender, e);
        }
    }

    public void LoadState()
    {
        var settings = _settingsService.CurrentSettings;
        TxtHotkeyDisplay.Text = $"{settings.HotkeyModifiers} + {settings.HotkeyKey}";
        ChkAutoStart.IsChecked = _settingsService.IsAutoStartEnabled();
    }

    private void OnStartSelectionClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        _startSelectionAction();
    }

    private void OnStartFreeformClick(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.DefaultMode = Core.Enums.SelectionMode.Freeform;
        _settingsService.SaveSettings();
        WindowState = WindowState.Minimized;
        _startSelectionAction();
    }

    private void OnStartRectangleClick(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.DefaultMode = Core.Enums.SelectionMode.Rectangle;
        _settingsService.SaveSettings();
        WindowState = WindowState.Minimized;
        _startSelectionAction();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _openSettingsAction();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        _isExplicitExit = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        _settingsService.SetAutoStart(ChkAutoStart.IsChecked == true);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            // Keep app readily accessible on the taskbar when user clicks Close (X)
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
        else
        {
            base.OnClosing(e);
        }
    }
}
