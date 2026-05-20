using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public enum CramPassCountingMode
{
    Cumulative = 0,
    Consecutive = 1,
}

public sealed record CrammingSettings(
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

    public bool HasAcceptedRating => CountHardAsPass || CountGoodAsPass || CountEasyAsPass;

    public bool IsAcceptedRating(FlashcardReviewRatingReadModel rating)
    {
        return rating switch
        {
            FlashcardReviewRatingReadModel.Hard => CountHardAsPass,
            FlashcardReviewRatingReadModel.Good => CountGoodAsPass,
            FlashcardReviewRatingReadModel.Easy => CountEasyAsPass,
            _ => false,
        };
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (RequiredPassCount is < MinimumRequiredPassCount or > MaximumRequiredPassCount)
        {
            errors.Add($"Required successful ratings must be between {MinimumRequiredPassCount} and {MaximumRequiredPassCount}.");
        }

        if (RetryAfterOtherCards is < MinimumRetryAfterOtherCards or > MaximumRetryAfterOtherCards)
        {
            errors.Add($"Retry spacing must be between {MinimumRetryAfterOtherCards} and {MaximumRetryAfterOtherCards} other cards.");
        }

        if (!HasAcceptedRating)
        {
            errors.Add("Choose at least one rating that counts as a pass.");
        }

        if (!Enum.IsDefined(PassCountingMode))
        {
            errors.Add("Choose a valid pass counting mode.");
        }

        return errors;
    }

    public CrammingSettings EnsureValid()
    {
        IReadOnlyList<string> errors = Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(CrammingSettings));
        }

        return this;
    }
}
