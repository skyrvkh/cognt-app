using System.Windows.Media;

namespace CogntApp.Design;

public static class BundledFontFamilyCatalog
{
    private static readonly Uri AppFontAssetsUri = new("pack://application:,,,/Assets/Fonts/");

    public static IReadOnlyList<string> Families { get; } =
    [
        "Inter",
        "Source Sans 3",
        "Source Serif 4",
        "IBM Plex Sans",
        "IBM Plex Serif",
        "IBM Plex Mono",
        "Roboto Flex",
        "Roboto Serif",
        "Public Sans",
        "Newsreader",
        "DM Sans",
        "Recursive",
        "Atkinson Hyperlegible Next",
        "Literata",
        "Fira Sans",
    ];

    public static string Normalize(string? value, string fallback)
    {
        string normalized = value?.Trim() ?? string.Empty;
        string? matchingFamily = Families.FirstOrDefault(
            family => string.Equals(family, normalized, StringComparison.OrdinalIgnoreCase));

        if (matchingFamily is not null)
        {
            return matchingFamily;
        }

        return Families.Contains(fallback, StringComparer.OrdinalIgnoreCase)
            ? Families.First(family => string.Equals(family, fallback, StringComparison.OrdinalIgnoreCase))
            : Families[0];
    }

    public static FontFamily CreateFontFamily(string? value)
    {
        string family = Normalize(value, EditorSettings.DefaultInterfaceFontFamily);
        return new FontFamily(AppFontAssetsUri, $"./#{family}");
    }
}
