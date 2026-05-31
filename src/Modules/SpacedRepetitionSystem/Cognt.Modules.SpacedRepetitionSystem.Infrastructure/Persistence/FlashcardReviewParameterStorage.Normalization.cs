using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal static partial class FlashcardReviewParameterStorage
{
    private static double[] Normalize(IReadOnlyList<double>? parameters)
    {
        double[] normalized = (parameters ?? [])
            .Where(double.IsFinite)
            .ToArray();
        if (normalized.Length != FsrsScheduler.DefaultParameters.Length)
        {
            return [];
        }

        for (int i = 0; i < normalized.Length; i++)
        {
            if (normalized[i] < FsrsScheduler.LowerBoundsParameters[i]
                || normalized[i] > FsrsScheduler.UpperBoundsParameters[i])
            {
                return [];
            }
        }

        return normalized;
    }
}
