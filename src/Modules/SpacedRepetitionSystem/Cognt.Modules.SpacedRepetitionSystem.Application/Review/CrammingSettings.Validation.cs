namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial record CrammingSettings
{
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
