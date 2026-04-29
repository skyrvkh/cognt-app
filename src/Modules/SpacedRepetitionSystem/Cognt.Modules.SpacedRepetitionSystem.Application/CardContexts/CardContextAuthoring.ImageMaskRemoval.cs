namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext UnlinkImageMask(
        CardContext context,
        string imageBlockId,
        string maskId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBlockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskId);

        List<CardTarget> targets = RemoveTargetLinksForImageMask([..context.Targets], imageBlockId.Trim(), maskId.Trim());
        List<CardContextImageSurface> images = [..context.Images];
        int imageIndex = images.FindIndex(image => string.Equals(image.ImageBlockId, imageBlockId, StringComparison.Ordinal));
        if (imageIndex >= 0)
        {
            CardContextImageSurface image = images[imageIndex];
            ImageOcclusionMask? currentMask = image.Masks.FirstOrDefault(mask => string.Equals(mask.Id, maskId, StringComparison.Ordinal));
            if (currentMask is not null)
            {
                images[imageIndex] = image with
                {
                    Masks = image.Masks
                        .Where(mask => !string.Equals(mask.Id, maskId, StringComparison.Ordinal))
                        .Concat([currentMask with { TargetId = string.Empty }])
                        .ToArray(),
                };
            }
        }

        return context with { Targets = targets, Images = images };
    }

    public static CardContext DeleteImageMask(
        CardContext context,
        string imageBlockId,
        string maskId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBlockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskId);

        List<CardTarget> targets = RemoveTargetLinksForImageMask([..context.Targets], imageBlockId.Trim(), maskId.Trim());
        List<CardContextImageSurface> images = [..context.Images];
        int imageIndex = images.FindIndex(image => string.Equals(image.ImageBlockId, imageBlockId, StringComparison.Ordinal));
        if (imageIndex >= 0)
        {
            CardContextImageSurface image = images[imageIndex];
            ImageOcclusionMask[] remainingMasks = image.Masks
                .Where(mask => !string.Equals(mask.Id, maskId, StringComparison.Ordinal))
                .ToArray();

            if (remainingMasks.Length == 0)
            {
                images.RemoveAt(imageIndex);
            }
            else
            {
                images[imageIndex] = image with { Masks = remainingMasks };
            }
        }

        return context with { Targets = targets, Images = images };
    }

    private static List<CardTarget> RemoveTargetLinksForImageMask(
        List<CardTarget> targets,
        string imageBlockId,
        string maskId)
    {
        for (int index = 0; index < targets.Count; index++)
        {
            CardTarget target = targets[index];
            ImageMaskAnchor[] anchors = target.ImageAnchors
                .Where(anchor =>
                    !string.Equals(anchor.ImageBlockId, imageBlockId, StringComparison.Ordinal)
                    || !string.Equals(anchor.MaskId, maskId, StringComparison.Ordinal))
                .ToArray();

            if (anchors.Length != target.ImageAnchors.Count)
            {
                targets[index] = target with { ImageAnchors = anchors };
            }
        }

        return targets;
    }
}
