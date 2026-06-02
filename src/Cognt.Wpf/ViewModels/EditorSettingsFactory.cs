using CogntApp.Design;

namespace CogntApp.ViewModels;

internal readonly record struct EditorSettingsDraft(
    bool IsAutoSaveEnabled,
    string AutoSaveIntervalMinutesText,
    bool IsSaveOnCloseEnabled,
    string GlobalTextFontSizeText,
    ApplicationTheme Theme,
    string InterfaceFontFamilyText,
    string DisplayFontFamilyText,
    string ReadingFontFamilyText,
    string CodeFontFamilyText,
    string EditorTextFontSizeText,
    string EditorHeadingFontSizeText,
    string EditorCodeFontSizeText,
    string EditorZoomPercentText);

internal static partial class EditorSettingsFactory
{
    public static EditorSettings CreateSnapshot(EditorSettingsDraft draft)
    {
        return new EditorSettings(
            draft.IsAutoSaveEnabled,
            GetValidatedAutoSaveIntervalMinutes(draft.AutoSaveIntervalMinutesText),
            draft.IsSaveOnCloseEnabled,
            GetValidatedNumber(
                draft.GlobalTextFontSizeText,
                EditorSettings.MinimumGlobalTextFontSize,
                EditorSettings.MaximumGlobalTextFontSize,
                EditorSettings.Default.GlobalTextFontSize),
            draft.Theme,
            draft.InterfaceFontFamilyText,
            draft.DisplayFontFamilyText,
            draft.ReadingFontFamilyText,
            draft.CodeFontFamilyText,
            GetValidatedNumber(
                draft.EditorTextFontSizeText,
                EditorSettings.MinimumEditorTextFontSize,
                EditorSettings.MaximumEditorTextFontSize,
                EditorSettings.DefaultEditorTextFontSize),
            GetValidatedNumber(
                draft.EditorHeadingFontSizeText,
                EditorSettings.MinimumEditorHeadingFontSize,
                EditorSettings.MaximumEditorHeadingFontSize,
                EditorSettings.DefaultEditorHeadingFontSize),
            GetValidatedNumber(
                draft.EditorCodeFontSizeText,
                EditorSettings.MinimumEditorCodeFontSize,
                EditorSettings.MaximumEditorCodeFontSize,
                EditorSettings.DefaultEditorCodeFontSize),
            GetValidatedNumber(
                draft.EditorZoomPercentText,
                EditorSettings.MinimumEditorZoomPercent,
                EditorSettings.MaximumEditorZoomPercent,
                EditorSettings.DefaultEditorZoomPercent)).Normalize();
    }

    public static bool TryCreateValidated(
        EditorSettingsDraft draft,
        out EditorSettings settings,
        out string validationMessage)
    {
        settings = EditorSettings.Default;
        validationMessage = string.Empty;

        if (!TryGetAutoSaveIntervalMinutes(draft.AutoSaveIntervalMinutesText, out int intervalMinutes, out validationMessage)
            || !TryGetEditorNumber(
                draft.GlobalTextFontSizeText,
                EditorSettings.MinimumGlobalTextFontSize,
                EditorSettings.MaximumGlobalTextFontSize,
                "global text size",
                out double globalTextSize,
                out validationMessage)
            || !TryGetEditorNumber(
                draft.EditorTextFontSizeText,
                EditorSettings.MinimumEditorTextFontSize,
                EditorSettings.MaximumEditorTextFontSize,
                "editor text size",
                out double editorTextSize,
                out validationMessage)
            || !TryGetEditorNumber(
                draft.EditorHeadingFontSizeText,
                EditorSettings.MinimumEditorHeadingFontSize,
                EditorSettings.MaximumEditorHeadingFontSize,
                "heading size",
                out double headingSize,
                out validationMessage)
            || !TryGetEditorNumber(
                draft.EditorCodeFontSizeText,
                EditorSettings.MinimumEditorCodeFontSize,
                EditorSettings.MaximumEditorCodeFontSize,
                "code size",
                out double codeSize,
                out validationMessage)
            || !TryGetEditorNumber(
                draft.EditorZoomPercentText,
                EditorSettings.MinimumEditorZoomPercent,
                EditorSettings.MaximumEditorZoomPercent,
                "editor zoom",
                out double zoomPercent,
                out validationMessage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(draft.InterfaceFontFamilyText)
            || string.IsNullOrWhiteSpace(draft.DisplayFontFamilyText)
            || string.IsNullOrWhiteSpace(draft.ReadingFontFamilyText)
            || string.IsNullOrWhiteSpace(draft.CodeFontFamilyText))
        {
            validationMessage = "Font family values cannot be empty.";
            return false;
        }

        settings = new EditorSettings(
            draft.IsAutoSaveEnabled,
            intervalMinutes,
            draft.IsSaveOnCloseEnabled,
            globalTextSize,
            draft.Theme,
            draft.InterfaceFontFamilyText,
            draft.DisplayFontFamilyText,
            draft.ReadingFontFamilyText,
            draft.CodeFontFamilyText,
            editorTextSize,
            headingSize,
            codeSize,
            zoomPercent).Normalize();
        return true;
    }

}
