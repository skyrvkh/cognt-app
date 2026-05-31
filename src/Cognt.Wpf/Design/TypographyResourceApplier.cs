using System.Windows;
using System.Windows.Media;

namespace CogntApp.Design;

internal static class TypographyResourceApplier
{
    internal static void Apply(EditorSettings settings)
    {
        Application? application = Application.Current;
        if (application is null)
        {
            return;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => Apply(settings));
            return;
        }

        EditorSettings normalized = settings.Normalize();
        FontFamily interfaceFontFamily = BundledFontFamilyCatalog.CreateFontFamily(normalized.InterfaceFontFamily);
        FontFamily displayFontFamily = BundledFontFamilyCatalog.CreateFontFamily(normalized.DisplayFontFamily);
        FontFamily readingFontFamily = BundledFontFamilyCatalog.CreateFontFamily(normalized.ReadingFontFamily);
        FontFamily codeFontFamily = BundledFontFamilyCatalog.CreateFontFamily(normalized.CodeFontFamily);

        ApplyToResources(
            application.Resources,
            normalized,
            interfaceFontFamily,
            displayFontFamily,
            readingFontFamily,
            codeFontFamily);

        foreach (Window window in application.Windows)
        {
            ApplyToResources(
                window.Resources,
                normalized,
                interfaceFontFamily,
                displayFontFamily,
                readingFontFamily,
                codeFontFamily);

            window.FontFamily = interfaceFontFamily;
            window.FontSize = normalized.GlobalTextFontSize;
        }
    }

    private static void ApplyToResources(
        ResourceDictionary resources,
        EditorSettings normalized,
        FontFamily interfaceFontFamily,
        FontFamily displayFontFamily,
        FontFamily readingFontFamily,
        FontFamily codeFontFamily)
    {
        SetResource(resources, "AppSansFontFamily", interfaceFontFamily);
        SetResource(resources, "AppDisplayFontFamily", displayFontFamily);
        SetResource(resources, "AppReadingFontFamily", readingFontFamily);
        SetResource(resources, "AppMonoFontFamily", codeFontFamily);

        SetResource(resources, "FontSizeCaption", Math.Max(9, normalized.GlobalTextFontSize - 1));
        SetResource(resources, "FontSizeSmall", Math.Max(10, normalized.GlobalTextFontSize));
        SetResource(resources, "FontSizeBody", Math.Max(10, normalized.GlobalTextFontSize + 1));
        SetResource(resources, "FontSizeBodyLarge", Math.Max(11, normalized.GlobalTextFontSize + 2));
        SetResource(resources, "FontSizePanelTitle", Math.Max(14, normalized.GlobalTextFontSize + 6));
        SetResource(resources, "FontSizeTitle", Math.Max(16, normalized.GlobalTextFontSize + 9));
        SetResource(resources, "FontSizePageTitle", Math.Max(20, normalized.GlobalTextFontSize + 17));
        SetResource(resources, "FontSizeHero", Math.Max(24, normalized.GlobalTextFontSize + 23));

        SetResource(resources, "LineHeightTight", Math.Max(14, normalized.GlobalTextFontSize + 6));
        SetResource(resources, "LineHeightBody", Math.Max(17, normalized.GlobalTextFontSize + 9));
        SetResource(resources, "LineHeightLarge", Math.Max(20, normalized.GlobalTextFontSize + 12));
        SetResource(resources, "LineHeightTitle", Math.Max(26, normalized.GlobalTextFontSize + 19));
        SetResource(resources, "LineHeightHero", Math.Max(34, normalized.GlobalTextFontSize + 31));
    }

    private static void SetResource(ResourceDictionary resources, string key, object value)
    {
        resources[key] = value;
    }
}
