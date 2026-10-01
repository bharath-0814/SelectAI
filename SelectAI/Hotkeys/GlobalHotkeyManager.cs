using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Interop;
using SelectAI.Core.Utils;
using SelectAI.Settings;

namespace SelectAI.Hotkeys;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle = IntPtr.Zero;
    private bool _isRegistered = false;

    public event EventHandler? HotkeyPressed;

    public void Initialize(HwndSource hwndSource)
    {
        _hwndSource = hwndSource;
        _windowHandle = hwndSource.Handle;
        _hwndSource.AddHook(HwndHook);
    }

    public bool Register(string modifiersStr, string keyStr)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        Unregister();

        uint fsModifiers = 0;
        if (modifiersStr.Contains("Control", StringComparison.OrdinalIgnoreCase))
            fsModifiers |= NativeMethods.MOD_CONTROL;
        if (modifiersStr.Contains("Shift", StringComparison.OrdinalIgnoreCase))
            fsModifiers |= NativeMethods.MOD_SHIFT;
        if (modifiersStr.Contains("Alt", StringComparison.OrdinalIgnoreCase))
            fsModifiers |= NativeMethods.MOD_ALT;
        if (modifiersStr.Contains("Win", StringComparison.OrdinalIgnoreCase))
            fsModifiers |= NativeMethods.MOD_WIN;

        fsModifiers |= NativeMethods.MOD_NOREPEAT;

        if (!Enum.TryParse<Key>(keyStr, true, out var key))
        {
            key = Key.Space;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, fsModifiers, (uint)vk);
        if (!_isRegistered)
        {
            Debug.WriteLine($"Failed to register hotkey {modifiersStr}+{keyStr}. (Error: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
        }

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }
}
