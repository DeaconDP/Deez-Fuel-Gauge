using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace DeezFuelGauge.Services;

/// <summary>
/// Clips a Win32 HWND to a client rect so a full-size transparent Avalonia window
/// can look and hit-test like a smaller pill without SetWindowPos size changes.
/// </summary>
internal static class WindowsWindowRegion
{
    public static bool TryClear(TopLevel topLevel)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var hwnd = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
            return false;

        SetWindowRgn(hwnd, IntPtr.Zero, true);
        return true;
    }

    public static bool TrySetLogicalClientRegion(TopLevel topLevel, Rect logicalRect, double renderScaling)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var hwnd = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
            return false;

        var scale = Math.Max(0.1, renderScaling);
        var left = (int)Math.Floor(logicalRect.X * scale);
        var top = (int)Math.Floor(logicalRect.Y * scale);
        var right = (int)Math.Ceiling((logicalRect.X + Math.Max(1, logicalRect.Width)) * scale);
        var bottom = (int)Math.Ceiling((logicalRect.Y + Math.Max(1, logicalRect.Height)) * scale);
        if (right <= left || bottom <= top)
            return false;

        var region = CreateRectRgn(left, top, right, bottom);
        if (region == IntPtr.Zero)
            return false;

        // SetWindowRgn takes ownership of the region on success.
        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            DeleteObject(region);
            return false;
        }

        return true;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
}
