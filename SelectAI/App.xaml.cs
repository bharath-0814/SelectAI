using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using SelectAI.AI;
using SelectAI.Capture;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Utils;
using SelectAI.Hotkeys;
using SelectAI.Ocr;
using SelectAI.Search;
using SelectAI.Settings;
using SelectAI.UI.Main;
using SelectAI.UI.Overlay;
using SelectAI.UI.Settings;
using SelectAI.UI.Tray;

namespace SelectAI;

public partial class App : System.Windows.Application
{
    private SettingsService? _settingsService;
    private ScreenCaptureService? _screenCapture;
    private WindowsMediaOcrProvider? _ocrProvider;
    private GoogleSearchProvider? _searchProvider;
    private AiProviderFactory? _aiProviderFactory;

    private OverlayWindow? _overlayWindow;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private GlobalHotkeyManager? _hotkeyManager;
    private TrayIconManager? _trayIconManager;

    private static System.Threading.Mutex? _singleInstanceMutex;
    private HwndSource? _messageHwndSource;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        const string mutexName = "SelectAI_SingleInstance_Mutex_9B87F1C4";
        _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            System.Windows.MessageBox.Show(
                "SelectAI is already running in your taskbar and system tray.\n\nPress Ctrl + Shift + Space anywhere to start selecting, or click the SelectAI icon on your taskbar.",
                "SelectAI is Already Running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Keep app running in background (taskbar / system tray)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            // 1. Initialize Core Services
            _settingsService = new SettingsService();
            _screenCapture = new ScreenCaptureService();
            _ocrProvider = new WindowsMediaOcrProvider();
            _searchProvider = new GoogleSearchProvider();
            _aiProviderFactory = new AiProviderFactory(_settingsService);

            // 2. Initialize Overlay Window
            _overlayWindow = new OverlayWindow(
                _screenCapture,
                _ocrProvider,
                _searchProvider,
                _aiProviderFactory,
                _settingsService);

            // 3. Setup Hidden Top-Level Window for Global Hotkeys
            var windowParams = new HwndSourceParameters("SelectAIMessageWindow")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
                Width = 0,
                Height = 0,
                ParentWindow = IntPtr.Zero
            };
            _messageHwndSource = new HwndSource(windowParams);

            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.Initialize(_messageHwndSource);
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            // Register default or configured hotkey
            var mods = _settingsService.CurrentSettings.HotkeyModifiers;
            var key = _settingsService.CurrentSettings.HotkeyKey;
            AppLog.Info($"Registering global hotkeys. Configured: {mods} + {key}");
            bool hotkeyRegistered = _hotkeyManager.Register(mods, key);
            AppLog.Info($"Global hotkeys registered: {hotkeyRegistered} (Ctrl+Shift+Space, Ctrl+Shift+S, Alt+Shift+S active)");

            // 4. Initialize System Tray
            _trayIconManager = new TrayIconManager(
                _settingsService,
                startSelectionAction: TriggerSelection,
                openSettingsAction: OpenSettings,
                openMainWindowAction: OpenMainWindow);

            // Always open MainWindow on normal launch so user sees the app on the taskbar and screen!
            bool isBackground = e.Args.Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));
            if (!isBackground)
            {
                _trayIconManager.ShowReadyNotification();
                OpenMainWindow();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("SelectAI failed during startup", ex);
            System.Windows.MessageBox.Show(
                $"SelectAI failed to start:\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                "SelectAI Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        AppLog.Info("App.OnHotkeyPressed received -> Dispatching TriggerSelection.");
        Dispatcher.BeginInvoke(new Action(TriggerSelection));
    }

    public void TriggerSelection()
    {
        try
        {
            AppLog.Info($"App.TriggerSelection invoked. Overlay exists: {_overlayWindow != null}");
            if (_overlayWindow != null)
            {
                if (_overlayWindow.IsVisible)
                {
                    _overlayWindow.CloseOverlay();
                }
                else
                {
                    _overlayWindow.StartSelection();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Error executing TriggerSelection", ex);
        }
    }

    public void OpenMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new SelectAI.UI.Main.MainWindow(
                _settingsService!,
                _hotkeyManager,
                startSelectionAction: TriggerSelection,
                openSettingsAction: OpenSettings);
            _mainWindow.Closed += (s, e) => _mainWindow = null;
            _mainWindow.Show();
        }
        else
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();
        }
    }

    public void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settingsService!, _hotkeyManager);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _messageHwndSource?.Dispose();
        _trayIconManager?.Dispose();
        _overlayWindow?.Close();
        _mainWindow?.Close();

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }

        base.OnExit(e);
    }
}
