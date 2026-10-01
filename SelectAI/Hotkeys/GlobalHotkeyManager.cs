using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SelectAI.Core.Utils;

namespace SelectAI.Hotkeys;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int HotkeyIdPrimary = 9001;
    private const int HotkeyIdSecondary = 9002;
    private const int HotkeyIdTertiary = 9003;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_SPACE = 0x20;
    private const int VK_S = 0x53;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle = IntPtr.Zero;

    // Track state of modifiers directly from hook
    private bool _ctrlDown = false;
    private bool _shiftDown = false;
    private bool _altDown = false;
    private bool _winDown = false;

    // Target keys
    private int _targetVk = VK_SPACE;
    private bool _reqCtrl = true;
    private bool _reqShift = true;
    private bool _reqAlt = false;
    private bool _reqWin = false;

    private DateTime _lastTriggerTime = DateTime.MinValue;

    public event EventHandler? HotkeyPressed;

    public void Initialize(HwndSource? hwndSource)
    {
        _hwndSource = hwndSource;
        if (_hwndSource != null)
        {
            _windowHandle = _hwndSource.Handle;
            _hwndSource.AddHook(HwndHook);
        }

        // Standard WPF thread-level message filter
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

        InstallKeyboardHook();
    }

    private void InstallKeyboardHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;

        IntPtr hMod = IntPtr.Zero;
        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule?.ModuleName != null)
            {
                hMod = GetModuleHandle(curModule.ModuleName);
            }
        }
        catch { }

        if (hMod == IntPtr.Zero)
        {
            hMod = GetModuleHandle(null);
        }

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);
        AppLog.Info($"Global LowLevelKeyboardHook installed: {_hookId != IntPtr.Zero} (hMod={hMod})");
    }

    public bool Register(string modifiersStr, string keyStr)
    {
        Unregister();

        _reqCtrl = modifiersStr.Contains("Control", StringComparison.OrdinalIgnoreCase);
        _reqShift = modifiersStr.Contains("Shift", StringComparison.OrdinalIgnoreCase);
        _reqAlt = modifiersStr.Contains("Alt", StringComparison.OrdinalIgnoreCase);
        _reqWin = modifiersStr.Contains("Win", StringComparison.OrdinalIgnoreCase);

        if (!Enum.TryParse<Key>(keyStr, true, out var key))
        {
            key = Key.Space;
        }

        _targetVk = KeyInterop.VirtualKeyFromKey(key);

        uint fsModifiers = 0;
        if (_reqCtrl) fsModifiers |= NativeMethods.MOD_CONTROL;
        if (_reqShift) fsModifiers |= NativeMethods.MOD_SHIFT;
        if (_reqAlt) fsModifiers |= NativeMethods.MOD_ALT;
        if (_reqWin) fsModifiers |= NativeMethods.MOD_WIN;
        fsModifiers |= NativeMethods.MOD_NOREPEAT;

        // Register Primary Hotkey on Thread Message Queue
        bool regThread1 = NativeMethods.RegisterHotKey(IntPtr.Zero, HotkeyIdPrimary, fsModifiers, (uint)_targetVk);
        AppLog.Info($"RegisterHotKey(Thread, Primary {_targetVk:X2}): {regThread1}");

        // Register Secondary Hotkey (Ctrl + Shift + S) as instant fallback
        bool regThread2 = NativeMethods.RegisterHotKey(
            IntPtr.Zero,
            HotkeyIdSecondary,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            (uint)VK_S);
        AppLog.Info($"RegisterHotKey(Thread, Secondary VK_S): {regThread2}");

        // Register Tertiary Hotkey (Alt + Shift + S)
        bool regThread3 = NativeMethods.RegisterHotKey(
            IntPtr.Zero,
            HotkeyIdTertiary,
            NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            (uint)VK_S);
        AppLog.Info($"RegisterHotKey(Thread, Tertiary Alt+Shift+S): {regThread3}");

        // Also register on window handle if available
        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.RegisterHotKey(_windowHandle, HotkeyIdPrimary, fsModifiers, (uint)_targetVk);
            NativeMethods.RegisterHotKey(
                _windowHandle,
                HotkeyIdSecondary,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
                (uint)VK_S);
        }

        return _hookId != IntPtr.Zero || regThread1 || regThread2;
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            // Track modifier states
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                if (vkCode is VK_CONTROL or VK_LCONTROL or VK_RCONTROL) _ctrlDown = true;
                else if (vkCode is VK_SHIFT or VK_LSHIFT or VK_RSHIFT) _shiftDown = true;
                else if (vkCode is VK_MENU or VK_LMENU or VK_RMENU) _altDown = true;
                else if (vkCode is VK_LWIN or VK_RWIN) _winDown = true;

                // Check triggers:
                // 1. Configured target key (e.g. Space) with required modifiers
                // 2. Fallback 'S' with Ctrl+Shift
                bool isTargetKey = (vkCode == _targetVk);
                bool isFallbackKey = (vkCode == VK_S);

                if (isTargetKey || isFallbackKey)
                {
                    bool ctrl = _ctrlDown || IsKeyDown(VK_CONTROL) || IsKeyDown(VK_LCONTROL) || IsKeyDown(VK_RCONTROL);
                    bool shift = _shiftDown || IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT);
                    bool alt = _altDown || IsKeyDown(VK_MENU) || IsKeyDown(VK_LMENU) || IsKeyDown(VK_RMENU);
                    bool win = _winDown || IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);

                    // Case A: User's chosen shortcut (default: Ctrl + Shift + Space)
                    if (isTargetKey && ctrl == _reqCtrl && shift == _reqShift && alt == _reqAlt && win == _reqWin)
                    {
                        AppLog.Info($"Hotkey triggered via WH_KEYBOARD_LL (TargetKey: {vkCode})");
                        OnTrigger();
                        return (IntPtr)1; // Consume key to prevent typing into foreground application
                    }

                    // Case B: Universal fallback shortcut (Ctrl + Shift + S)
                    if (isFallbackKey && ctrl && shift && !alt && !win)
                    {
                        AppLog.Info("Hotkey triggered via WH_KEYBOARD_LL (Fallback: Ctrl+Shift+S)");
                        OnTrigger();
                        return (IntPtr)1;
                    }

                    // Case C: Universal fallback shortcut (Alt + Shift + S)
                    if (isFallbackKey && alt && shift && !ctrl && !win)
                    {
                        AppLog.Info("Hotkey triggered via WH_KEYBOARD_LL (Fallback: Alt+Shift+S)");
                        OnTrigger();
                        return (IntPtr)1;
                    }
                }
            }
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                if (vkCode is VK_CONTROL or VK_LCONTROL or VK_RCONTROL) _ctrlDown = false;
                else if (vkCode is VK_SHIFT or VK_LSHIFT or VK_RSHIFT) _shiftDown = false;
                else if (vkCode is VK_MENU or VK_LMENU or VK_RMENU) _altDown = false;
                else if (vkCode is VK_LWIN or VK_RWIN) _winDown = false;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void OnTrigger()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTriggerTime).TotalMilliseconds < 350)
        {
            return; // Debounce duplicate triggers
        }
        _lastTriggerTime = now;

        AppLog.Info("GlobalHotkeyManager: Firing HotkeyPressed event to listeners.");
        try
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Error("Error executing HotkeyPressed handler", ex);
        }
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message == NativeMethods.WM_HOTKEY)
        {
            int id = msg.wParam.ToInt32();
            if (id is HotkeyIdPrimary or HotkeyIdSecondary or HotkeyIdTertiary)
            {
                AppLog.Info($"Hotkey triggered via ComponentDispatcher WM_HOTKEY (id={id})");
                OnTrigger();
                handled = true;
            }
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id is HotkeyIdPrimary or HotkeyIdSecondary or HotkeyIdTertiary)
            {
                AppLog.Info($"Hotkey triggered via HwndHook WM_HOTKEY (id={id})");
                OnTrigger();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Unregister()
    {
        try
        {
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyIdPrimary);
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyIdSecondary);
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyIdTertiary);

            if (_windowHandle != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, HotkeyIdPrimary);
                NativeMethods.UnregisterHotKey(_windowHandle, HotkeyIdSecondary);
                NativeMethods.UnregisterHotKey(_windowHandle, HotkeyIdTertiary);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        Unregister();

        ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        if (_hwndSource != null && _windowHandle != IntPtr.Zero)
        {
            try
            {
                _hwndSource.RemoveHook(HwndHook);
            }
            catch { }
        }
    }
}
