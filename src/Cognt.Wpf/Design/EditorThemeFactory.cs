using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.Design;

internal static class EditorThemeFactory
{
    internal static EditorTheme Create(EditorSettings settings)
    {
        EditorSettings normalized = settings.Normalize();
        ApplicationTheme theme = normalized.Theme;

        return new EditorTheme(
            Name: theme.ToString(),
            Mode: theme == ApplicationTheme.Graphite ? "dark" : "light",
            Page: ThemeResourceLookup.ColorHex("EditorPageBrush", "#FFFFFF"),
            Surface: ThemeResourceLookup.ColorHex("EditorSurfaceBrush", "#FFFFFF"),
            Panel: ThemeResourceLookup.ColorHex("EditorPanelBrush", "#F8FAFF"),
            PanelAlt: ThemeResourceLookup.ColorHex("EditorPanelAltBrush", "#F2F5FF"),
            Line: ThemeResourceLookup.ColorHex("EditorLineBrush", "#DDE4EF"),
            Ink: ThemeResourceLookup.ColorHex("EditorInkBrush", "#142033"),
            Muted: ThemeResourceLookup.ColorHex("EditorMutedBrush", "#667085"),
            Accent: ThemeResourceLookup.ColorHex("AccentBrush", "#5867FF"),
            AccentStrong: ThemeResourceLookup.ColorHex("AccentStrongBrush", "#4254E8"),
            AccentSoft: ThemeResourceLookup.ColorHex("AccentSoftBrush", "#EEF2FF"),
            Selection: ThemeResourceLookup.ColorHex("EditorSelectionBrush", "#DDE7FF"),
            Cloze: ThemeResourceLookup.ColorHex("EditorClozeBrush", "#DBE4EE"),
            InterfaceFontFamily: normalized.InterfaceFontFamily,
            DisplayFontFamily: normalized.DisplayFontFamily,
            ReadingFontFamily: normalized.ReadingFontFamily,
            CodeFontFamily: normalized.CodeFontFamily,
            EditorTextFontSize: normalized.EditorTextFontSize,
            EditorHeadingFontSize: normalized.EditorHeadingFontSize,
            EditorCodeFontSize: normalized.EditorCodeFontSize,
            EditorZoomPercent: normalized.EditorZoomPercent);
    }
}
