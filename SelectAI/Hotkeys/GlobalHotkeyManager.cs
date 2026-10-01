using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SelectAI.Core.Utils;

namespace SelectAI.Hotkeys;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int HotkeyId = 9001;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

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
    private bool _isRegistered = false;

    private int _targetVk = 0x20; // Default: Space
    private bool _reqCtrl = true;
    private bool _reqShift = true;
    private bool _reqAlt = false;
    private bool _reqWin = false;

    private DateTime _lastTriggerTime = DateTime.MinValue;

    public event EventHandler? HotkeyPressed;

    public void Initialize(HwndSource hwndSource)
    {
        _hwndSource = hwndSource;
        _windowHandle = hwndSource.Handle;
        _hwndSource.AddHook(HwndHook);

        InstallKeyboardHook();
    }

    private void InstallKeyboardHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule?.ModuleName), 0);
        Debug.WriteLine($"LowLevelKeyboardHook installed: {_hookId != IntPtr.Zero}");
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

        // Also attempt standard RegisterHotKey on top-level HWND
        if (_windowHandle != IntPtr.Zero)
        {
            uint fsModifiers = 0;
            if (_reqCtrl) fsModifiers |= NativeMethods.MOD_CONTROL;
            if (_reqShift) fsModifiers |= NativeMethods.MOD_SHIFT;
            if (_reqAlt) fsModifiers |= NativeMethods.MOD_ALT;
            if (_reqWin) fsModifiers |= NativeMethods.MOD_WIN;
            fsModifiers |= NativeMethods.MOD_NOREPEAT;

            _isRegistered = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, fsModifiers, (uint)_targetVk);
        }

        return _hookId != IntPtr.Zero || _isRegistered;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == _targetVk)
            {
                bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                bool win = ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0) || ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0);

                if (ctrl == _reqCtrl && shift == _reqShift && alt == _reqAlt && win == _reqWin)
                {
                    OnTrigger();
                    return (IntPtr)1; // Consume key to prevent typing into focused app
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void OnTrigger()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTriggerTime).TotalMilliseconds < 400)
        {
            return; // Debounce
        }
        _lastTriggerTime = now;

        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            OnTrigger();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
        }
    }

    public void Dispose()
    {
        Unregister();

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _hookProc = null;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }
}
