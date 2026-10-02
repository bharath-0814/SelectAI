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
    private const string WakeupEventName = "SelectAI_Wakeup_Event_9B87F1C4";
    private EventWaitHandle? _wakeupEvent;
    private System.Threading.Thread? _wakeupListenerThread;
    private volatile bool _isAppExiting = false;

    private HwndSource? _messageHwndSource;
    private const string ShowAppWindowMessage = "SelectAI_ShowMainWindow_Message_8829";
    public static uint WmShowAppMessage { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EnsureSelfInstalled();

        WmShowAppMessage = NativeMethods.RegisterWindowMessage(ShowAppWindowMessage);

        const string mutexName = "SelectAI_SingleInstance_Mutex_9B87F1C4";
        bool isNewInstance = false;
        try
        {
            _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out isNewInstance);
            if (!isNewInstance)
            {
                // Verify whether another SelectAI process is genuinely alive
                int currentPid = Environment.ProcessId;
                var otherProcesses = Process.GetProcessesByName("SelectAI").Where(p => p.Id != currentPid).ToList();
                if (otherProcesses.Count == 0)
                {
                    AppLog.Info("Mutex was held by an exited or dead process. Reclaiming primary instance.");
                    isNewInstance = true;
                }
            }
        }
        catch (AbandonedMutexException)
        {
            AppLog.Info("Mutex was abandoned by a terminated process. Taking ownership as primary instance.");
            isNewInstance = true;
        }

        if (!isNewInstance)
        {
            AppLog.Info("Another active instance of SelectAI detected. Signaling wakeup event and bringing window forward.");

            try
            {
                using var wakeupEv = EventWaitHandle.OpenExisting(WakeupEventName);
                wakeupEv.Set();
                AppLog.Info("Successfully signaled SelectAI wakeup event.");
            }
            catch (Exception ex)
            {
                AppLog.Info($"Could not open existing wakeup event: {ex.Message}");
            }

            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, WmShowAppMessage, IntPtr.Zero, IntPtr.Zero);

            try
            {
                int curPid = Environment.ProcessId;
                var otherProc = Process.GetProcessesByName("SelectAI").FirstOrDefault(p => p.Id != curPid);
                if (otherProc != null && otherProc.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(otherProc.MainWindowHandle, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(otherProc.MainWindowHandle);
                }
            }
            catch { }

            Shutdown();
            return;
        }

        // Start Wakeup Event Listener thread in primary instance
        try
        {
            _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeupEventName);
            _wakeupListenerThread = new System.Threading.Thread(() =>
            {
                while (!_isAppExiting)
                {
                    try
                    {
                        if (_wakeupEvent.WaitOne(400))
                        {
                            if (_isAppExiting) break;
                            AppLog.Info("Wakeup event received from another instance -> bringing MainWindow forward.");
                            Dispatcher.BeginInvoke(new Action(OpenMainWindow));
                        }
                    }
                    catch (System.Threading.ThreadAbortException) { break; }
                    catch { }
                }
            })
            {
                IsBackground = true,
                Name = "SelectAI_WakeupListener"
            };
            _wakeupListenerThread.Start();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to initialize WakeupEvent listener", ex);
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
                ParentWindow = NativeMethods.HWND_MESSAGE
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
            AppLog.Info($"Global hotkeys registered: {hotkeyRegistered} (Ctrl+Shift+C, Ctrl+Shift+S, Ctrl+Shift+Space, Alt+Shift+C active)");

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

    private void EnsureSelfInstalled()
    {
        string? currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe)) return;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string targetDir = System.IO.Path.Combine(localAppData, "Programs", "SelectAI");
        string targetExe = System.IO.Path.Combine(targetDir, "SelectAI.exe");

        if (currentExe.Equals(targetExe, StringComparison.OrdinalIgnoreCase))
        {
            // Already running from installed location
            return;
        }

        AppLog.Info($"Installing app from {currentExe} to {targetExe}");

        try
        {
            System.IO.Directory.CreateDirectory(targetDir);
            
            // Wait for existing process to exit if we are updating
            var existingProcs = Process.GetProcessesByName("SelectAI").Where(p => p.Id != Environment.ProcessId).ToList();
            foreach (var p in existingProcs)
            {
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }

            // Clean target directory to prevent conflicts with legacy loose DLLs
            try
            {
                foreach (var oldFile in System.IO.Directory.GetFiles(targetDir))
                {
                    try { System.IO.File.Delete(oldFile); } catch { }
                }
            }
            catch { }

            // Copy all files from current dir to target dir
            string sourceDir = System.IO.Path.GetDirectoryName(currentExe)!;
            foreach (var file in System.IO.Directory.GetFiles(sourceDir, "*", System.IO.SearchOption.AllDirectories))
            {
                string relPath = file.Substring(sourceDir.Length + 1);
                string destFile = System.IO.Path.Combine(targetDir, relPath);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destFile)!);
                System.IO.File.Copy(file, destFile, true);
            }

            // Create Shortcuts
            CreateShortcut(targetExe, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SelectAI");
            string startMenuDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            CreateShortcut(targetExe, startMenuDir, "SelectAI");
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            CreateShortcut(targetExe, startupDir, "SelectAI");

            AppLog.Info("Installation complete, restarting from target location.");
            Process.Start(targetExe);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to install app", ex);
        }
    }

    private void CreateShortcut(string targetPath, string directory, string linkName)
    {
        string shortcutPath = System.IO.Path.Combine(directory, linkName + ".lnk");
        try
        {
            string psScript = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{shortcutPath}');$s.TargetPath='{targetPath}';$s.WorkingDirectory='{System.IO.Path.GetDirectoryName(targetPath)}';$s.Save()";
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{psScript}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to create shortcut at {shortcutPath}", ex);
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
        AppLog.Info($"OpenMainWindow called. Existing window: {_mainWindow != null}, Loaded: {_mainWindow?.IsLoaded}");
        try
        {
            if (_mainWindow == null || !_mainWindow.IsLoaded)
            {
                _mainWindow = new SelectAI.UI.Main.MainWindow(
                    _settingsService!,
                    _hotkeyManager,
                    startSelectionAction: TriggerSelection,
                    openSettingsAction: OpenSettings);
                MainWindow = _mainWindow;
                _mainWindow.Closed += (s, e) => _mainWindow = null;
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
            _mainWindow.Focus();

            var hwnd = new WindowInteropHelper(_mainWindow).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
            }

            AppLog.Info($"OpenMainWindow completed. HWND: 0x{hwnd:X}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Error opening MainWindow", ex);
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
        _isAppExiting = true;
        try { _wakeupEvent?.Set(); } catch { }
        _wakeupEvent?.Dispose();

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
