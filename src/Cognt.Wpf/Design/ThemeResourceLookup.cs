using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CogntApp.Design;

internal static class ThemeResourceLookup
{
    internal static Brush Brush(string key, Brush fallback)
    {
        object? resource = Application.Current?.TryFindResource(key);
        return resource as Brush ?? fallback;
    }

    internal static FontFamily FontFamily(string key, string fallback)
    {
        object? resource = Application.Current?.TryFindResource(key);
        return resource as FontFamily ?? new FontFamily(fallback);
    }

    internal static SolidColorBrush SolidBrush(string key, string fallbackColor)
    {
        Color color = ColorValue(key, fallbackColor);
        return new SolidColorBrush(color);
    }

    internal static SolidColorBrush SolidBrush(string key, string fallbackColor, double opacity)
    {
        SolidColorBrush brush = SolidBrush(key, fallbackColor);
        brush.Opacity = opacity;
        return brush;
    }

    internal static string ColorHex(string key, string fallbackColor)
    {
        Color color = ColorValue(key, fallbackColor);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    internal static Color ColorValue(string key, string fallbackColor)
    {
        object? resource = Application.Current?.TryFindResource(key);
        return resource switch
        {
            SolidColorBrush brush => brush.Color,
            Color color => color,
            _ => (Color)ColorConverter.ConvertFromString(fallbackColor)!
        };
    }
}
