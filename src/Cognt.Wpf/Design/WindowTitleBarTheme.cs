using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CogntApp.Design;

internal static class WindowTitleBarTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    internal static void Apply(Window window, ApplicationTheme theme)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        try
        {
            int darkMode = theme == ApplicationTheme.Graphite ? 1 : 0;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkMode,
                ref darkMode,
                Marshal.SizeOf<int>());

            int backdrop = 2;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaSystemBackdropType,
                ref backdrop,
                Marshal.SizeOf<int>());

            SetColor(handle, DwmwaCaptionColor, ThemeResourceLookup.ColorValue("TitleBarBrush", "#F3F6FB"));
            SetColor(handle, DwmwaTextColor, ThemeResourceLookup.ColorValue("TitleBarTextBrush", "#142033"));
            SetColor(handle, DwmwaBorderColor, ThemeResourceLookup.ColorValue("TitleBarBorderBrush", "#DDE4EF"));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static void SetColor(nint handle, int attribute, Color color)
    {
        int colorRef = color.R | (color.G << 8) | (color.B << 16);
        _ = DwmSetWindowAttribute(handle, attribute, ref colorRef, Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
