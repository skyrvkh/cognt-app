using System.Globalization;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal static partial class FlashcardReviewStepStorage
{
    internal static string Serialize(
        IReadOnlyList<TimeSpan>? steps,
        IReadOnlyList<TimeSpan> fallback)
    {
        TimeSpan[] normalized = Normalize(steps, fallback);
        return string.Join(
            ',',
            normalized.Select(step => Math.Max(1, (long)Math.Round(step.TotalSeconds)).ToString(CultureInfo.InvariantCulture)));
    }

    internal static IReadOnlyList<TimeSpan> Deserialize(
        string? value,
        IReadOnlyList<TimeSpan> fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback.ToArray();
        }

        List<TimeSpan> steps = [];
        foreach (string token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds)
                || seconds <= 0)
            {
                return fallback.ToArray();
            }

            steps.Add(TimeSpan.FromSeconds(seconds));
        }

        return steps.Count > 0
            ? steps.ToArray()
            : fallback.ToArray();
    }
}
