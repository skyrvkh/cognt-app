namespace CogntApp.Design;

public sealed record EditorSettings(bool IsAutoSaveEnabled, int AutoSaveIntervalMinutes)
{
    public const int MinimumAutoSaveIntervalMinutes = 1;
    public const int MaximumAutoSaveIntervalMinutes = 60;

    public static EditorSettings Default { get; } = new(false, 1);

    public EditorSettings Normalize()
    {
        return this with
        {
            AutoSaveIntervalMinutes = Math.Clamp(
                AutoSaveIntervalMinutes,
                MinimumAutoSaveIntervalMinutes,
                MaximumAutoSaveIntervalMinutes),
        };
    }
}
