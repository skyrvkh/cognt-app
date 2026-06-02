using System.Globalization;
using CogntApp.Design;

namespace CogntApp.ViewModels;

internal static partial class EditorSettingsFactory
{
    public static int GetValidatedAutoSaveIntervalMinutes(string text)
    {
        return TryGetAutoSaveIntervalMinutes(text, out int intervalMinutes, out _)
            ? intervalMinutes
            : EditorSettings.Default.AutoSaveIntervalMinutes;
    }

    private static bool TryGetAutoSaveIntervalMinutes(
        string text,
        out int intervalMinutes,
        out string validationMessage)
    {
        validationMessage = string.Empty;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalMinutes))
        {
            validationMessage = "Enter a whole number of minutes.";
            return false;
        }

        if (intervalMinutes < EditorSettings.MinimumAutoSaveIntervalMinutes
            || intervalMinutes > EditorSettings.MaximumAutoSaveIntervalMinutes)
        {
            validationMessage = $"Use a value from {EditorSettings.MinimumAutoSaveIntervalMinutes} to {EditorSettings.MaximumAutoSaveIntervalMinutes} minutes.";
            return false;
        }

        return true;
    }

    private static double GetValidatedNumber(string text, double minimum, double maximum, double fallback)
    {
        return TryGetEditorNumber(text, minimum, maximum, string.Empty, out double value, out _)
            ? value
            : fallback;
    }

    private static bool TryGetEditorNumber(
        string text,
        double minimum,
        double maximum,
        string label,
        out double value,
        out string validationMessage)
    {
        value = 0;
        validationMessage = string.Empty;
        if (!double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            validationMessage = string.IsNullOrWhiteSpace(label)
                ? "Enter a number."
                : $"Enter a number for {label}.";
            return false;
        }

        value = Math.Round(value, 0);
        if (value < minimum || value > maximum)
        {
            validationMessage = string.IsNullOrWhiteSpace(label)
                ? $"Use a value from {minimum:0} to {maximum:0}."
                : $"Use a {label} from {minimum:0} to {maximum:0}.";
            return false;
        }

        return true;
    }
}
