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
            ImageAnchors = ReplaceImageAnchorForMask(target.ImageAnchors, anchor),
        };

        CardContext updated = context with { Targets = targets };
        updated = UpsertMaskOnImageSurface(updated, anchor.ImageBlockId, imageRelPath, normalizedMask);
        return updated;
    }

    private static ImageMaskAnchor[] ReplaceImageAnchorForMask(
        IReadOnlyList<ImageMaskAnchor> anchors,
        ImageMaskAnchor replacement)
    {
        HashSet<string> seenAnchorIds = new(StringComparer.Ordinal);
        ImageMaskAnchor[] nextAnchors = new ImageMaskAnchor[anchors.Count + 1];
        int count = 0;

        foreach (ImageMaskAnchor existing in anchors)
        {
            if (string.Equals(existing.MaskId, replacement.MaskId, StringComparison.Ordinal))
            {
                continue;
            }

            if (seenAnchorIds.Add(existing.Id))
            {
                nextAnchors[count++] = existing;
            }
        }

        if (seenAnchorIds.Add(replacement.Id))
        {
            nextAnchors[count++] = replacement;
        }

        if (count != nextAnchors.Length)
        {
            Array.Resize(ref nextAnchors, count);
        }

        return nextAnchors;
    }
}
