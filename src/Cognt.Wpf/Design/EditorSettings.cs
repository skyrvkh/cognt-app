namespace CogntApp.Design;

public sealed record EditorSettings(
    bool IsAutoSaveEnabled,
    int AutoSaveIntervalMinutes,
    bool IsSaveOnCloseEnabled = true,
    double GlobalTextFontSize = 11,
    ApplicationTheme Theme = ApplicationTheme.Light)
{
    public const int MinimumAutoSaveIntervalMinutes = 1;
    public const int MaximumAutoSaveIntervalMinutes = 60;
    public const double MinimumGlobalTextFontSize = 9;
    public const double MaximumGlobalTextFontSize = 18;

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
        };
    }
}
