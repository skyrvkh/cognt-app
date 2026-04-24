using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static ImageMaskReadModel? CreateImageMaskPresentationModel(
        ImageOcclusionMask mask,
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool isAnswerVisible)
    {
        bool isCurrentGroup = string.Equals(slotId, currentSlotId, StringComparison.Ordinal);
        if (isAnswerVisible)
        {
            if (ShouldRevealSlotOnBack(slotId, currentSlotId, presentation))
            {
                return CreateImageMaskReadModel(mask, slotId, FlashcardSemanticStyle.ResolvedTarget, isHidden: false);
            }

            return presentation.ImageFrontMode switch
            {
                ImageOcclusionFrontMode.HideAllMasksCueCurrent => CreateImageMaskReadModel(
                    mask,
                    slotId,
                    isCurrentGroup ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.SiblingTarget,
                    isHidden: true),
                ImageOcclusionFrontMode.HideAllMasksSameStyle => CreateImageMaskReadModel(
                    mask,
                    slotId,
                    FlashcardSemanticStyle.HiddenTarget,
                    isHidden: true),
                ImageOcclusionFrontMode.ShowVisibleMasksAsOutlines => CreateImageMaskReadModel(
                    mask,
                    slotId,
                    isCurrentGroup ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.SiblingTarget,
                    isHidden: false),
                _ => null,
            };
        }

        return presentation.ImageFrontMode switch
        {
            ImageOcclusionFrontMode.None or ImageOcclusionFrontMode.ShowNoMasks => null,
            ImageOcclusionFrontMode.HideCurrentMaskOnly when !isCurrentGroup => null,
            ImageOcclusionFrontMode.HideCurrentMaskOnly => CreateImageMaskReadModel(
                mask,
                slotId,
                presentation.CueCurrentTarget ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.HiddenTarget,
                isHidden: true),
            ImageOcclusionFrontMode.HideAllMasksCueCurrent => CreateImageMaskReadModel(
                mask,
                slotId,
                ResolveHiddenTargetStyle(isCurrentGroup, presentation.CueCurrentTarget),
                isHidden: true),
            ImageOcclusionFrontMode.HideAllMasksSameStyle => CreateImageMaskReadModel(
                mask,
                slotId,
                FlashcardSemanticStyle.HiddenTarget,
                isHidden: true),
            ImageOcclusionFrontMode.ShowVisibleMasksAsOutlines => CreateImageMaskReadModel(
                mask,
                slotId,
                isCurrentGroup ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.SiblingTarget,
                isHidden: false),
            _ => null,
        };
    }

    private static ImageMaskReadModel CreateImageMaskReadModel(
        ImageOcclusionMask mask,
        string slotId,
        FlashcardSemanticStyle style,
        bool isHidden)
    {
        return new ImageMaskReadModel(
            mask.Id,
            mask.TargetId,
            mask.Shape,
            mask.X,
            mask.Y,
            mask.Width,
            mask.Height,
            mask.Cx,
            mask.Cy,
            mask.Rx,
            mask.Ry,
            mask.Points,
            mask.Label,
            slotId,
            style,
            isHidden);
    }
}
