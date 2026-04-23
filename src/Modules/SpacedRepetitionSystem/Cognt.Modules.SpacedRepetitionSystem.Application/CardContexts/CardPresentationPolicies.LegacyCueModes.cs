namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardPresentationPolicies
{
    public static CardPresentationPolicy? FromLegacyCueModes(CardCueMode? textCueMode, CardCueMode? imageCueMode)
    {
        if (textCueMode is null && imageCueMode is null)
        {
            return null;
        }

        TextClozeFrontMode textFrontMode = textCueMode switch
        {
            CardCueMode.HideCurrentSlotOnly => TextClozeFrontMode.HideCurrentShowSiblings,
            CardCueMode.HideAllSlotsCueCurrent => TextClozeFrontMode.HideAllCueCurrent,
            CardCueMode.HideAllSlotsSameStyle => TextClozeFrontMode.HideAllSameStyle,
            _ => AppDefault.TextFrontMode,
        };
        ImageOcclusionFrontMode imageFrontMode = imageCueMode switch
        {
            CardCueMode.HideCurrentSlotOnly => ImageOcclusionFrontMode.HideCurrentMaskOnly,
            CardCueMode.HideAllSlotsCueCurrent => ImageOcclusionFrontMode.HideAllMasksCueCurrent,
            CardCueMode.HideAllSlotsSameStyle => ImageOcclusionFrontMode.HideAllMasksSameStyle,
            _ => ImageOcclusionFrontMode.None,
        };

        CardPresentationPreset preset = (textFrontMode, imageFrontMode) switch
        {
            (TextClozeFrontMode.HideCurrentShowSiblings, ImageOcclusionFrontMode.None) =>
                CardPresentationPreset.TextHideCurrentShowSiblings,
            (TextClozeFrontMode.HideAllCueCurrent, ImageOcclusionFrontMode.None) =>
                CardPresentationPreset.TextHideAllCueCurrent,
            (TextClozeFrontMode.HideCurrentShowSiblings, ImageOcclusionFrontMode.HideCurrentMaskOnly) =>
                CardPresentationPreset.TextHideCurrentShowSiblingsAndCueCurrentImageMask,
            (TextClozeFrontMode.HideAllCueCurrent, ImageOcclusionFrontMode.HideCurrentMaskOnly) =>
                CardPresentationPreset.TextHideAllCueCurrentAndCueCurrentImageMask,
            (TextClozeFrontMode.HideCurrentShowSiblings, ImageOcclusionFrontMode.HideAllMasksCueCurrent) =>
                CardPresentationPreset.TextHideCurrentShowSiblingsAndImageHideAllCueCurrent,
            (TextClozeFrontMode.HideAllCueCurrent, ImageOcclusionFrontMode.HideAllMasksCueCurrent) =>
                CardPresentationPreset.TextHideAllCueCurrentAndImageHideAllCueCurrent,
            _ => CardPresentationPreset.Custom,
        };

        return new CardPresentationPolicy(
            preset,
            textFrontMode,
            imageFrontMode,
            CardBackRevealMode.RevealAllSlots,
            CueCurrentTarget: true,
            RevealSiblingAnswersOnBack: true);
    }
}
