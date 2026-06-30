using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Thiccdal.Teleprompter.Display;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _callbacks = new Dictionary<int, Action>();
    private bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public bool Register(int id, string hotkeyString, Action callback)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HotkeyManager));
        }

        var (modifiers, virtualKey) = ParseHotkeyString(hotkeyString);

        if (!RegisterHotKey(_hwnd, id, modifiers, virtualKey))
        {
            return false;
        }

        _callbacks[id] = callback;
        return true;
    }

    public bool Unregister(int id)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HotkeyManager));
        }

        if (!UnregisterHotKey(_hwnd, id))
        {
            return false;
        }

        _callbacks.Remove(id);
        return true;
    }

    public void UnregisterAll()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var id in _callbacks.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }

        _callbacks.Clear();
    }

    public IntPtr ProcessMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();

            if (_callbacks.TryGetValue(id, out var callback))
            {
                callback.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static (uint Modifiers, uint VirtualKey) ParseHotkeyString(string hotkeyString)
    {
        uint modifiers = 0;
        uint virtualKey = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var normalizedPart = part.ToUpperInvariant();

            switch (normalizedPart)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (Enum.TryParse<Key>(part, ignoreCase: true, out var key))
                    {
                        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                    }
                    break;
            }
        }

        return (modifiers, virtualKey);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        _disposed = true;
    }
}
