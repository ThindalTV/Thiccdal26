using System.Runtime.InteropServices;
using System.Windows;

namespace Thiccdal.Teleprompter.Display;

/// <summary>
/// Helper class for enumerating monitors and positioning windows using Win32 APIs.
/// </summary>
public static partial class MonitorHelper
{
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>
    /// Gets the total number of monitors connected to the system.
    /// </summary>
    public static int GetMonitorCount()
    {
        return GetAllMonitorHandles().Count;
    }

    /// <summary>
    /// Gets the bounds of the monitor at the specified index.
    /// </summary>
    /// <param name="index">Zero-based monitor index.</param>
    /// <returns>The bounds of the specified monitor, or primary monitor bounds if index is invalid.</returns>
    public static Rect GetMonitorBounds(int index)
    {
        var handles = GetAllMonitorHandles();

        if (index < 0 || index >= handles.Count)
        {
            return GetPrimaryMonitorBounds();
        }

        return GetMonitorBoundsFromHandle(handles[index]);
    }

    /// <summary>
    /// Returns true if more than one monitor is connected.
    /// </summary>
    public static bool HasMultipleMonitors()
    {
        return GetMonitorCount() > 1;
    }

    /// <summary>
    /// Gets the bounds of all connected monitors.
    /// </summary>
    public static List<Rect> GetAllMonitorBounds()
    {
        var handles = GetAllMonitorHandles();
        var bounds = new List<Rect>(handles.Count);

        foreach (var handle in handles)
        {
            bounds.Add(GetMonitorBoundsFromHandle(handle));
        }

        return bounds;
    }

    /// <summary>
    /// Gets the combined bounds of all monitors except the one at the specified index.
    /// Useful for creating a mouse-blocking region that excludes the teleprompter display.
    /// </summary>
    /// <param name="excludeIndex">Zero-based index of the monitor to exclude.</param>
    /// <returns>Combined bounding rectangle of all non-excluded monitors.</returns>
    public static Rect GetNonTargetMonitorBounds(int excludeIndex)
    {
        var allBounds = GetAllMonitorBounds();

        if (allBounds.Count == 0)
        {
            return Rect.Empty;
        }

        if (allBounds.Count == 1)
        {
            return Rect.Empty;
        }

        var combinedBounds = Rect.Empty;

        for (var i = 0; i < allBounds.Count; i++)
        {
            if (i == excludeIndex)
            {
                continue;
            }

            if (combinedBounds.IsEmpty)
            {
                combinedBounds = allBounds[i];
            }
            else
            {
                combinedBounds.Union(allBounds[i]);
            }
        }

        return combinedBounds;
    }

    private static List<IntPtr> GetAllMonitorHandles()
    {
        var monitors = new List<IntPtr>();

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                monitors.Add(hMonitor);
                return true;
            },
            IntPtr.Zero);

        return monitors;
    }

    private static Rect GetMonitorBoundsFromHandle(IntPtr hMonitor)
    {
        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return new Rect(
                monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Top,
                monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);
        }

        return GetPrimaryMonitorBounds();
    }

    private static Rect GetPrimaryMonitorBounds()
    {
        return new Rect(
            0,
            0,
            SystemParameters.PrimaryScreenWidth,
            SystemParameters.PrimaryScreenHeight);
    }
}
