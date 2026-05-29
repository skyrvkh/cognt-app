namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public enum CramPassCountingMode
{
    Cumulative = 0,
    Consecutive = 1,
}

public sealed partial record CrammingSettings(
    int RequiredPassCount,
    bool CountHardAsPass,
    bool CountGoodAsPass,
    bool CountEasyAsPass,
    CramPassCountingMode PassCountingMode,
    int RetryAfterOtherCards,
    bool ShuffleRemainingCards,
    bool AffectNormalSchedule)
{
    public const int MinimumRequiredPassCount = 1;
    public const int MaximumRequiredPassCount = 100;
    public const int MinimumRetryAfterOtherCards = 0;
    public const int MaximumRetryAfterOtherCards = 100;

    public static CrammingSettings Default { get; } = new(
        3,
        CountHardAsPass: true,
        CountGoodAsPass: true,
        CountEasyAsPass: false,
        CramPassCountingMode.Consecutive,
        3,
        ShuffleRemainingCards: true,
        AffectNormalSchedule: false);
}
