using System.Globalization;

namespace CogntApp.ViewModels;

internal static partial class ReviewSettingsFactory
{
    public static string FormatRetention(double retention)
    {
        double percent = Math.Clamp(retention, 0, 1) * 100;
        return percent.ToString("0", CultureInfo.InvariantCulture);
    }

    public static bool TryParseRetention(string? value, out double retention)
    {
        retention = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().TrimEnd('%').Trim();
        if (!double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out double parsed))
        {
            return false;
        }

        retention = parsed > 1
            ? parsed / 100d
            : parsed;
        return true;
    }
}
