using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static ImageOcclusionReadModel? BuildImageOcclusionReadModel(
        IReadOnlyList<CardContextImageSurface>? images,
        IReadOnlyDictionary<string, string> targetSlots,
        string currentSlotId,
        CardPresentationPolicy presentation,
        string? collectionRoot,
        bool isAnswerVisible,
        out bool missingMedia)
    {
        missingMedia = false;
        if (images is null || images.Count == 0)
        {
            return null;
        }

        List<ImageMaskReadModel> masks = [];
        string? firstImageRelPath = null;
        string? firstImageFullPath = null;
        foreach (CardContextImageSurface image in images)
        {
            firstImageRelPath ??= image.ImageRelPath;
            string? resolvedImagePath = ResolveCollectionMediaPath(collectionRoot, image.ImageRelPath);
            firstImageFullPath ??= resolvedImagePath;
            if (resolvedImagePath is null || !File.Exists(resolvedImagePath))
            {
                missingMedia = true;
            }

            foreach (ImageOcclusionMask mask in image.Masks)
            {
                if (!targetSlots.TryGetValue(mask.TargetId, out string? slotId))
                {
                    continue;
                }

                ImageMaskReadModel? renderedMask = CreateImageMaskPresentationModel(
                    mask,
                    slotId,
                    currentSlotId,
                    presentation,
                    isAnswerVisible);
                if (renderedMask is not null)
                {
                    masks.Add(renderedMask);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(firstImageRelPath))
        {
            return null;
        }

        return new ImageOcclusionReadModel(firstImageRelPath, firstImageFullPath, masks);
    }

}
