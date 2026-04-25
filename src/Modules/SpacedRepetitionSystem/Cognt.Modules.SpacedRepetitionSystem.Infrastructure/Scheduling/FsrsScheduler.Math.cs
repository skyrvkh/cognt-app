namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;

internal sealed partial class FsrsScheduler
{
    private static double ClampDifficulty(double difficulty)
    {
        return Math.Min(Math.Max(difficulty, MinDifficulty), MaxDifficulty);
    }

    private static double ClampStability(double stability)
    {
        return Math.Max(stability, StabilityMin);
    }

    private double InitialStability(FsrsRating rating)
    {
        return ClampStability(Parameters[(int)rating - 1]);
    }

    private double InitialDifficulty(FsrsRating rating, bool clamp)
    {
        double initialDifficulty = Parameters[4] - Math.Pow(Math.E, Parameters[5] * ((int)rating - 1)) + 1;
        return clamp ? ClampDifficulty(initialDifficulty) : initialDifficulty;
    }

    private int NextInterval(double stability)
    {
        double nextInterval = stability / factor * (Math.Pow(DesiredRetention, 1 / decay) - 1);
        int rounded = (int)Math.Round(nextInterval);
        rounded = Math.Max(rounded, 1);
        rounded = Math.Min(rounded, MaximumInterval);
        return rounded;
    }

    private double ShortTermStability(double stability, FsrsRating rating)
    {
        double increase = Math.Pow(Math.E, Parameters[17] * ((int)rating - 3 + Parameters[18])) * Math.Pow(stability, -Parameters[19]);

        if (rating is FsrsRating.Good or FsrsRating.Easy)
        {
            increase = Math.Max(increase, 1.0);
        }

        return ClampStability(stability * increase);
    }

    private double NextDifficulty(double difficulty, FsrsRating rating)
    {
        double arg1 = InitialDifficulty(FsrsRating.Easy, clamp: false);
        double deltaDifficulty = -(Parameters[6] * ((int)rating - 3));
        double arg2 = difficulty + (10.0 - difficulty) * deltaDifficulty / 9.0;
        double nextDifficulty = Parameters[7] * arg1 + (1 - Parameters[7]) * arg2;
        return ClampDifficulty(nextDifficulty);
    }

    private double NextForgetStability(double difficulty, double stability, double retrievability)
    {
        double longTerm = Parameters[11]
            * Math.Pow(difficulty, -Parameters[12])
            * (Math.Pow(stability + 1, Parameters[13]) - 1)
            * Math.Pow(Math.E, (1 - retrievability) * Parameters[14]);

        double shortTerm = stability / Math.Pow(Math.E, Parameters[17] * Parameters[18]);
        return Math.Min(longTerm, shortTerm);
    }

    private double NextRecallStability(double difficulty, double stability, double retrievability, FsrsRating rating)
    {
        double hardPenalty = rating == FsrsRating.Hard ? Parameters[15] : 1;
        double easyBonus = rating == FsrsRating.Easy ? Parameters[16] : 1;

        return stability * (1
            + Math.Pow(Math.E, Parameters[8])
            * (11 - difficulty)
            * Math.Pow(stability, -Parameters[9])
            * (Math.Pow(Math.E, (1 - retrievability) * Parameters[10]) - 1)
            * hardPenalty
            * easyBonus);
    }

    private double NextStability(double difficulty, double stability, double retrievability, FsrsRating rating)
    {
        double nextStability = rating == FsrsRating.Again
            ? NextForgetStability(difficulty, stability, retrievability)
            : NextRecallStability(difficulty, stability, retrievability, rating);

        return ClampStability(nextStability);
    }
}
