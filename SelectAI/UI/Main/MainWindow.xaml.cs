using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;
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

        PreviewKeyDown += OnPreviewKeyDown;
        LoadState();
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
