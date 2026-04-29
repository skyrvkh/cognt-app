namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext AttachImageMaskToTarget(
        CardContext context,
        string targetId,
        ImageMaskAnchor anchor,
        ImageOcclusionMask mask,
        string imageRelPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageRelPath);

        List<CardTarget> targets = [..context.Targets];
        int targetIndex = targets.FindIndex(target => string.Equals(target.Id, targetId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            throw new InvalidOperationException($"Card context target '{targetId}' does not exist.");
        }

        ImageOcclusionMask normalizedMask = mask with { TargetId = targetId.Trim() };
        targets = RemoveTargetLinksForImageMask(targets, anchor.ImageBlockId, anchor.MaskId);

        CardTarget target = targets[targetIndex];
        targets[targetIndex] = target with
        {
            ImageAnchors = target.ImageAnchors
                .Where(existing => !string.Equals(existing.MaskId, anchor.MaskId, StringComparison.Ordinal))
                .Concat([anchor])
                .DistinctBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray(),
        };

        CardContext updated = context with { Targets = targets };
        updated = UpsertMaskOnImageSurface(updated, anchor.ImageBlockId, imageRelPath, normalizedMask);
        return updated;
    }

}
