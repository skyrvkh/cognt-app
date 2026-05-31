namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal static partial class FlashcardReviewStepStorage
{
    private static TimeSpan[] Normalize(
        IReadOnlyList<TimeSpan>? steps,
        IReadOnlyList<TimeSpan> fallback)
    {
        TimeSpan[] normalized = (steps ?? [])
            .Where(step => step > TimeSpan.Zero)
            .ToArray();

        return normalized.Length > 0
            ? normalized
            : fallback.ToArray();
    }
}
