namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static IReadOnlyList<CardTarget> ResolveTargets(
        CardSlot slot,
        IReadOnlyList<CardTarget> allTargets)
    {
        if (slot.TargetIds.Count == 0)
        {
            return allTargets
                .Where(target => string.Equals(target.SlotId, slot.Id, StringComparison.Ordinal))
                .ToArray();
        }

        Dictionary<string, CardTarget> targetsById = allTargets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

        return slot.TargetIds
            .Select(targetId => targetsById.TryGetValue(targetId, out CardTarget? target) ? target : null)
            .Where(target => target is not null)
            .Select(target => target!)
            .ToArray();
    }

    private static IReadOnlyList<TextAnchorPreviewReadModel> BuildTextAnchorPreviews(IReadOnlyList<CardTarget> targets)
    {
        return targets
            .SelectMany(target => target.TextAnchors.Select(anchor =>
                new TextAnchorPreviewReadModel(target.Id, anchor.Id, anchor.BlockId, anchor.MarkId)))
            .ToArray();
    }

    private static IReadOnlyList<ImageMaskPreviewReadModel> BuildImageAnchorPreviews(
        CardContext context,
        IReadOnlyList<CardTarget> targets)
    {
        Dictionary<string, CardContextImageSurface> imagesByBlockId = context.Images
            .ToDictionary(image => image.ImageBlockId, StringComparer.Ordinal);

        return targets
            .SelectMany(target => target.ImageAnchors.Select(anchor =>
            {
                imagesByBlockId.TryGetValue(anchor.ImageBlockId, out CardContextImageSurface? image);
                ImageOcclusionMask? mask = image?.Masks.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, anchor.MaskId, StringComparison.Ordinal));

                return new ImageMaskPreviewReadModel(
                    target.Id,
                    anchor.Id,
                    anchor.ImageBlockId,
                    anchor.MaskId,
                    image?.ImageRelPath,
                    mask?.Label);
            }))
            .ToArray();
    }

    private static string BuildAnswerPreview(IReadOnlyList<CardTarget> targets)
    {
        return string.Join(
            ", ",
            targets
                .Select(target => target.Answer)
                .Where(answer => !string.IsNullOrWhiteSpace(answer))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
