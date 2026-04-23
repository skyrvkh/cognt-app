namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardPresentationPolicies
{
    public static IReadOnlyList<CardPresentationPreset> UserSelectablePresets { get; } =
    [
        CardPresentationPreset.TextHideAllCueCurrent,
        CardPresentationPreset.TextHideAllCueCurrentAndCueCurrentImageMask,
        CardPresentationPreset.TextHideCurrentShowSiblings,
        CardPresentationPreset.TextHideCurrentShowSiblingsAndImageHideAllCueCurrent,
        CardPresentationPreset.TextHideAllCueCurrentAndImageHideAllCueCurrent,
        CardPresentationPreset.TextHideCurrentShowSiblingsAndCueCurrentImageMask,
        CardPresentationPreset.Custom,
    ];

    public static CardPresentationPolicy AppDefault { get; } = new(
        CardPresentationPreset.TextHideCurrentShowSiblings,
        TextClozeFrontMode.HideCurrentShowSiblings,
        ImageOcclusionFrontMode.None,
        CardBackRevealMode.RevealCurrentSlotOnly,
        CueCurrentTarget: true,
        RevealSiblingAnswersOnBack: false);

    public static CardPresentationPolicy ForPreset(CardPresentationPreset preset)
    {
        return preset switch
        {
            CardPresentationPreset.TextHideAllCueCurrent => new CardPresentationPolicy(
                preset,
                TextClozeFrontMode.HideAllCueCurrent,
                ImageOcclusionFrontMode.None,
                CardBackRevealMode.RevealAllSlots,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: true),
            CardPresentationPreset.TextHideAllCueCurrentAndCueCurrentImageMask => new CardPresentationPolicy(
                preset,
                TextClozeFrontMode.HideAllCueCurrent,
                ImageOcclusionFrontMode.HideCurrentMaskOnly,
                CardBackRevealMode.RevealAllSlots,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: true),
            CardPresentationPreset.TextHideCurrentShowSiblings => AppDefault,
            CardPresentationPreset.TextHideCurrentShowSiblingsAndImageHideAllCueCurrent => new CardPresentationPolicy(
                preset,
                TextClozeFrontMode.HideCurrentShowSiblings,
                ImageOcclusionFrontMode.HideAllMasksCueCurrent,
                CardBackRevealMode.RevealCurrentTargetOnly,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: false),
            CardPresentationPreset.TextHideAllCueCurrentAndImageHideAllCueCurrent => new CardPresentationPolicy(
                preset,
                TextClozeFrontMode.HideAllCueCurrent,
                ImageOcclusionFrontMode.HideAllMasksCueCurrent,
                CardBackRevealMode.RevealAllSlots,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: true),
            CardPresentationPreset.TextHideCurrentShowSiblingsAndCueCurrentImageMask => new CardPresentationPolicy(
                preset,
                TextClozeFrontMode.HideCurrentShowSiblings,
                ImageOcclusionFrontMode.HideCurrentMaskOnly,
                CardBackRevealMode.RevealCurrentTargetOnly,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: false),
            _ => AppDefault with { Preset = CardPresentationPreset.Custom },
        };
    }
}
