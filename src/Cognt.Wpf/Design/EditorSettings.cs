namespace CogntApp.Design;

public sealed record EditorSettings(
    bool IsAutoSaveEnabled,
    int AutoSaveIntervalMinutes,
    bool IsSaveOnCloseEnabled = true,
    double GlobalTextFontSize = 11,
    ApplicationTheme Theme = ApplicationTheme.Light,
    string InterfaceFontFamily = EditorSettings.DefaultInterfaceFontFamily,
    string DisplayFontFamily = EditorSettings.DefaultDisplayFontFamily,
    string ReadingFontFamily = EditorSettings.DefaultReadingFontFamily,
    string CodeFontFamily = EditorSettings.DefaultCodeFontFamily,
    double EditorTextFontSize = EditorSettings.DefaultEditorTextFontSize,
    double EditorHeadingFontSize = EditorSettings.DefaultEditorHeadingFontSize,
    double EditorCodeFontSize = EditorSettings.DefaultEditorCodeFontSize,
    double EditorZoomPercent = EditorSettings.DefaultEditorZoomPercent)
{
    public const int MinimumAutoSaveIntervalMinutes = 1;
    public const int MaximumAutoSaveIntervalMinutes = 60;
    public const double MinimumGlobalTextFontSize = 9;
    public const double MaximumGlobalTextFontSize = 18;
    public const double MinimumEditorTextFontSize = 10;
    public const double MaximumEditorTextFontSize = 28;
    public const double MinimumEditorHeadingFontSize = 18;
    public const double MaximumEditorHeadingFontSize = 72;
    public const double MinimumEditorCodeFontSize = 9;
    public const double MaximumEditorCodeFontSize = 24;
    public const double MinimumEditorZoomPercent = 50;
    public const double MaximumEditorZoomPercent = 200;
    public const double DefaultEditorTextFontSize = 15;
    public const double DefaultEditorHeadingFontSize = 40;
    public const double DefaultEditorCodeFontSize = 13;
    public const double DefaultEditorZoomPercent = 100;
    public const string DefaultInterfaceFontFamily = "Inter";
    public const string DefaultDisplayFontFamily = "Source Serif 4";
    public const string DefaultReadingFontFamily = "Source Serif 4";
    public const string DefaultCodeFontFamily = "Recursive";

    public static EditorSettings Default { get; } = new(false, 1, true, 11, ApplicationTheme.Light);

    public EditorSettings Normalize()
    {
        return this with
        {
            AutoSaveIntervalMinutes = Math.Clamp(
                AutoSaveIntervalMinutes,
                MinimumAutoSaveIntervalMinutes,
                MaximumAutoSaveIntervalMinutes),
            GlobalTextFontSize = Math.Clamp(
                Math.Round(GlobalTextFontSize, 0),
                MinimumGlobalTextFontSize,
                MaximumGlobalTextFontSize),
            Theme = Enum.IsDefined(Theme)
                ? Theme
                : ApplicationTheme.Light,
            InterfaceFontFamily = NormalizeFontFamily(InterfaceFontFamily, DefaultInterfaceFontFamily),
            DisplayFontFamily = NormalizeFontFamily(DisplayFontFamily, DefaultDisplayFontFamily),
            ReadingFontFamily = NormalizeFontFamily(ReadingFontFamily, DefaultReadingFontFamily),
            CodeFontFamily = NormalizeFontFamily(CodeFontFamily, DefaultCodeFontFamily),
            EditorTextFontSize = ClampRounded(EditorTextFontSize, MinimumEditorTextFontSize, MaximumEditorTextFontSize),
            EditorHeadingFontSize = ClampRounded(EditorHeadingFontSize, MinimumEditorHeadingFontSize, MaximumEditorHeadingFontSize),
            EditorCodeFontSize = ClampRounded(EditorCodeFontSize, MinimumEditorCodeFontSize, MaximumEditorCodeFontSize),
            EditorZoomPercent = ClampRounded(EditorZoomPercent, MinimumEditorZoomPercent, MaximumEditorZoomPercent),
        };
    }

    private static double ClampRounded(double value, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = minimum;
        }

        return Math.Clamp(Math.Round(value, 0), minimum, maximum);
    }

    private static string NormalizeFontFamily(string? value, string fallback)
    {
        return BundledFontFamilyCatalog.Normalize(value, fallback);
    }
}
