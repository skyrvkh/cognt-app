namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static CandidateValidation ValidateCandidate(
        CardContext context,
        CardSlot slot,
        IReadOnlyList<CardTarget> targets,
        IReadOnlySet<string> availableBlockIds,
        IReadOnlySet<int> duplicateDisplayIndexes)
    {
        if (context.Status == CardContextStatus.Disabled)
        {
            return CandidateValidation.Valid();
        }

        if (slot.IsDisabled)
        {
            return CandidateValidation.Valid();
        }

        if (slot.DisplayClozeIndex <= 0)
        {
            return CandidateValidation.Invalid("Display cloze indexes must be positive.");
        }

        if (duplicateDisplayIndexes.Contains(slot.DisplayClozeIndex))
        {
            return CandidateValidation.Invalid("Display cloze indexes must be unique within a card context.");
        }

        if (targets.Count == 0)
        {
            return CandidateValidation.Invalid("This card slot does not contain any card targets yet.");
        }

        foreach (CardTarget target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Answer))
            {
                return CandidateValidation.Invalid("Each card target needs an answer.");
            }

            if (target.TextAnchors.Count == 0 && target.ImageAnchors.Count == 0)
            {
                return CandidateValidation.Draft("Add at least one text anchor or image mask before generating a card.");
            }

            foreach (TextAnchor anchor in target.TextAnchors)
            {
                if (string.IsNullOrWhiteSpace(anchor.BlockId) || !availableBlockIds.Contains(anchor.BlockId))
                {
                    return CandidateValidation.Invalid("A text anchor points to a note block that no longer exists.");
                }
            }

            foreach (ImageMaskAnchor anchor in target.ImageAnchors)
            {
                if (string.IsNullOrWhiteSpace(anchor.ImageBlockId) || !availableBlockIds.Contains(anchor.ImageBlockId))
                {
                    return CandidateValidation.Invalid("An image anchor points to an image block that no longer exists.");
                }

                CardContextImageSurface? image = context.Images.FirstOrDefault(surface =>
                    string.Equals(surface.ImageBlockId, anchor.ImageBlockId, StringComparison.Ordinal));
                if (image is null)
                {
                    return CandidateValidation.Invalid("An image anchor refers to a missing image surface.");
                }

                ImageOcclusionMask? mask = image.Masks.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, anchor.MaskId, StringComparison.Ordinal));
                if (mask is null)
                {
                    return CandidateValidation.Invalid("An image anchor refers to a missing mask.");
                }

                if (!string.Equals(mask.TargetId, target.Id, StringComparison.Ordinal))
                {
                    return CandidateValidation.Invalid("An image mask is linked to the wrong recall target.");
                }

                if (!HasValidImageMaskCoordinates(mask))
                {
                    return CandidateValidation.Invalid("An image mask has invalid normalized coordinates.");
                }
            }
        }

        return CandidateValidation.Valid();
    }

    private readonly record struct CandidateValidation(bool IsValid, bool IsDraft, string? Message)
    {
        internal static CandidateValidation Valid() => new(true, false, null);

        internal static CandidateValidation Draft(string message) => new(false, true, message);

        internal static CandidateValidation Invalid(string message) => new(false, false, message);
    }
}
