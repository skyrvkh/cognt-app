using System.Globalization;
using System.Text;

namespace CogntApp.ViewModels;

internal static partial class ReviewSettingsFactory
{
    public static string FormatSteps(IReadOnlyList<TimeSpan>? steps)
    {
        if (steps is null || steps.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (TimeSpan step in steps)
        {
            if (step <= TimeSpan.Zero)
            {
                continue;
            }

            AppendDelimitedValue(builder, FormatStep(step));
        }

        return builder.ToString();
    }

    private static string FormatStep(TimeSpan step)
    {
        if (step.TotalDays >= 1 && IsWholeNumber(step.TotalDays))
        {
            return $"{step.TotalDays:0}d";
        }

        if (step.TotalHours >= 1 && IsWholeNumber(step.TotalHours))
        {
            return $"{step.TotalHours:0}h";
        }

        if (step.TotalMinutes >= 1 && IsWholeNumber(step.TotalMinutes))
        {
            return $"{step.TotalMinutes:0}m";
        }

        return $"{Math.Max(1, Math.Round(step.TotalSeconds)):0}s";
    }

    private static bool IsWholeNumber(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001d;
    }

    private static bool TryParseSteps(string? value, out TimeSpan[] steps)
    {
        steps = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens = value.Split(
            [',', ';', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<TimeSpan> parsedSteps = [];

        foreach (string token in tokens)
        {
            if (!TryParseStep(token, out TimeSpan step))
            {
                return false;
            }

            parsedSteps.Add(step);
        }

        if (parsedSteps.Count == 0)
        {
            return false;
        }

        steps = parsedSteps.ToArray();
        return true;
    }

    private static bool TryParseStep(string token, out TimeSpan step)
    {
        step = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalized = token.Trim().ToLowerInvariant();
        char suffix = normalized[^1];
        string numberText = char.IsLetter(suffix)
            ? normalized[..^1]
            : normalized;

        if (!double.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out double number)
            || number <= 0)
        {
            return false;
        }

        step = suffix switch
        {
            's' => TimeSpan.FromSeconds(number),
            'm' => TimeSpan.FromMinutes(number),
            'h' => TimeSpan.FromHours(number),
            'd' => TimeSpan.FromDays(number),
            _ when !char.IsLetter(suffix) => TimeSpan.FromMinutes(number),
            _ => TimeSpan.Zero,
        };

        return step > TimeSpan.Zero;
    }
}
