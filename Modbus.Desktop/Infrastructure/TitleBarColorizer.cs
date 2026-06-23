using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Runtime.InteropServices;

namespace Modbus.Desktop.Infrastructure;

/// <summary>
/// Colors the native Windows 11 title bar (caption) of a window via DWM, keeping
/// the system min/maximize/close buttons. No-op on non-Windows or pre-Win11 (build &lt; 22000).
/// </summary>
public static class TitleBarColorizer
{
    private const int DWMWA_CAPTION_COLOR = 35; // Win11 22000+
    private const int DWMWA_TEXT_COLOR    = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Sets the caption background and text color. Safe to call repeatedly.</summary>
    public static void Apply(Window window, Color caption, Color text)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        try
        {
            var captionRef = ToColorRef(caption);
            var textRef    = ToColorRef(text);
            DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionRef, sizeof(int));
            DwmSetWindowAttribute(handle, DWMWA_TEXT_COLOR,    ref textRef,    sizeof(int));
        }
        catch { /* non-critical cosmetic feature */ }
    }

    // Win32 COLORREF is 0x00BBGGRR.
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
