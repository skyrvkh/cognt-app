namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext UpsertMaskOnImageSurface(
        CardContext context,
        string imageBlockId,
        string imageRelPath,
        ImageOcclusionMask mask)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBlockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageRelPath);

        List<CardContextImageSurface> images = [..context.Images];
        int imageIndex = images.FindIndex(image => string.Equals(image.ImageBlockId, imageBlockId, StringComparison.Ordinal));
        if (imageIndex < 0)
        {
            images.Add(new CardContextImageSurface(imageBlockId.Trim(), CardContextDraftNoteDocument.NormalizeRelPath(imageRelPath), [mask]));
        }
        else
        {
            CardContextImageSurface image = images[imageIndex];
            images[imageIndex] = image with
            {
                ImageRelPath = CardContextDraftNoteDocument.NormalizeRelPath(imageRelPath),
                Masks = ReplaceImageMaskById(image.Masks, mask),
            };
        }

        return context with
        {
            Images = images,
            ContextBlockIds = AppendDistinctOrdinal(context.ContextBlockIds, imageBlockId.Trim()),
        };
    }

    public static CardContext SetImageMaskDisabled(
        CardContext context,
        string imageBlockId,
        string maskId,
        bool isDisabled)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<CardContextImageSurface> images = [..context.Images];
        int imageIndex = images.FindIndex(image => string.Equals(image.ImageBlockId, imageBlockId, StringComparison.Ordinal));
        if (imageIndex < 0)
        {
            return context;
        }

        CardContextImageSurface image = images[imageIndex];
        images[imageIndex] = image with
        {
            Masks = SetImageMaskDisabled(image.Masks, maskId, isDisabled),
        };

        return context with { Images = images };
    }

    private static ImageOcclusionMask[] ReplaceImageMaskById(
        IReadOnlyList<ImageOcclusionMask> masks,
        ImageOcclusionMask replacement)
    {
        ImageOcclusionMask[] nextMasks = new ImageOcclusionMask[masks.Count + 1];
        int count = 0;

        foreach (ImageOcclusionMask existing in masks)
        {
            if (!string.Equals(existing.Id, replacement.Id, StringComparison.Ordinal))
            {
                nextMasks[count++] = existing;
            }
        }

        nextMasks[count++] = replacement;
        if (count != nextMasks.Length)
        {
            Array.Resize(ref nextMasks, count);
        }

        return nextMasks;
    }

    private static ImageOcclusionMask[] SetImageMaskDisabled(
        IReadOnlyList<ImageOcclusionMask> masks,
        string maskId,
        bool isDisabled)
    {
        ImageOcclusionMask[] nextMasks = new ImageOcclusionMask[masks.Count];
        for (int index = 0; index < masks.Count; index++)
        {
            ImageOcclusionMask mask = masks[index];
            nextMasks[index] = string.Equals(mask.Id, maskId, StringComparison.Ordinal)
                ? mask with { IsDisabled = isDisabled }
                : mask;
        }

        return nextMasks;
    }
}
