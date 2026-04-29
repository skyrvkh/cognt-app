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
                Masks = image.Masks
                    .Where(existing => !string.Equals(existing.Id, mask.Id, StringComparison.Ordinal))
                    .Concat([mask])
                    .ToArray(),
            };
        }

        return context with
        {
            Images = images,
            ContextBlockIds = context.ContextBlockIds
                .Concat([imageBlockId.Trim()])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
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
            Masks = image.Masks
                .Select(mask => string.Equals(mask.Id, maskId, StringComparison.Ordinal)
                    ? mask with { IsDisabled = isDisabled }
                    : mask)
                .ToArray(),
        };

        return context with { Images = images };
    }
}
