using System.Runtime.InteropServices;
using System.Windows;

namespace Thiccdal.Teleprompter.Display;

/// <summary>
/// Prevents the mouse cursor from entering a specific monitor's area using a low-level mouse hook.
/// </summary>
public sealed class MouseBlocker : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelMouseProc? _hookCallback;
    private Rect _blockedArea;
    private RECT _originalClipRect;
    private bool _hadOriginalClip;
    private bool _disposed;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(IntPtr lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClipCursor(out RECT lpRect);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    public bool IsBlocking => _hookId != IntPtr.Zero;

    public void Start(Rect blockedArea)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsBlocking)
        {
            Stop();
        }

        _blockedArea = blockedArea;

        _hadOriginalClip = GetClipCursor(out _originalClipRect);

        _hookCallback = HookCallback;

        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = GetModuleHandle(curModule?.ModuleName);

        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookCallback, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install mouse hook. Error code: {error}");
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _hookCallback = null;

        if (_hadOriginalClip)
        {
            ClipCursor(ref _originalClipRect);
        }
        else
        {
            ClipCursor(IntPtr.Zero);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WM_MOUSEMOVE)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var mousePoint = new System.Windows.Point(hookStruct.pt.X, hookStruct.pt.Y);

            if (_blockedArea.Contains(mousePoint))
            {
                var clampedX = mousePoint.X;
                var clampedY = mousePoint.Y;

                var distanceToLeft = Math.Abs(mousePoint.X - _blockedArea.Left);
                var distanceToRight = Math.Abs(mousePoint.X - _blockedArea.Right);
                var distanceToTop = Math.Abs(mousePoint.Y - _blockedArea.Top);
                var distanceToBottom = Math.Abs(mousePoint.Y - _blockedArea.Bottom);

                var minHorizontal = Math.Min(distanceToLeft, distanceToRight);
                var minVertical = Math.Min(distanceToTop, distanceToBottom);

                if (minHorizontal <= minVertical)
                {
                    clampedX = distanceToLeft < distanceToRight
                        ? _blockedArea.Left - 1
                        : _blockedArea.Right;
                }
                else
                {
                    clampedY = distanceToTop < distanceToBottom
                        ? _blockedArea.Top - 1
                        : _blockedArea.Bottom;
                }

                var clipRect = new RECT
                {
                    Left = (int)clampedX,
                    Top = (int)clampedY,
                    Right = (int)clampedX + 1,
                    Bottom = (int)clampedY + 1
                };

                ClipCursor(ref clipRect);
                ClipCursor(IntPtr.Zero);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
