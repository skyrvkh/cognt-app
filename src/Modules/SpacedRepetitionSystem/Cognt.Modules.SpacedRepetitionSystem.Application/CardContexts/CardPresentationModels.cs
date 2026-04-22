using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed record CardPresentationPolicy(
    [property: JsonPropertyName("preset")]
    CardPresentationPreset Preset,
    [property: JsonPropertyName("textFrontMode")]
    TextClozeFrontMode TextFrontMode,
    [property: JsonPropertyName("imageFrontMode")]
    ImageOcclusionFrontMode ImageFrontMode,
    [property: JsonPropertyName("backRevealMode")]
    CardBackRevealMode BackRevealMode,
    [property: JsonPropertyName("cueCurrentTarget")]
    bool CueCurrentTarget,
    [property: JsonPropertyName("revealSiblingAnswersOnBack")]
    bool RevealSiblingAnswersOnBack);

public enum CardPresentationPreset
{
    TextHideAllCueCurrent = 0,
    TextHideCurrentShowSiblings = 1,
    TextHideCurrentShowSiblingsAndCueCurrentImageMask = 2,
    TextHideCurrentShowSiblingsAndImageHideAllCueCurrent = 3,
    TextHideAllCueCurrentAndCueCurrentImageMask = 4,
    TextHideAllCueCurrentAndImageHideAllCueCurrent = 5,
    Custom = 6,

    [Obsolete("Use TextHideAllCueCurrent.")]
    TextHideAllCueCurrentRevealAll = TextHideAllCueCurrent,

    [Obsolete("Use TextHideCurrentShowSiblingsAndCueCurrentImageMask.")]
    TextAndImageHideCurrent = TextHideCurrentShowSiblingsAndCueCurrentImageMask,

    [Obsolete("Use TextHideCurrentShowSiblingsAndImageHideAllCueCurrent.")]
    ImageHideAllCueCurrent = TextHideCurrentShowSiblingsAndImageHideAllCueCurrent,
}

public enum TextClozeFrontMode
{
    None = 0,
    HideCurrentShowSiblings = 1,
    HideAllCueCurrent = 2,
    HideAllSameStyle = 3,
    ShowAll = 4,
}

public enum ImageOcclusionFrontMode
{
    None = 0,
    HideCurrentMaskOnly = 1,
    HideAllMasksCueCurrent = 2,
    HideAllMasksSameStyle = 3,
    ShowVisibleMasksAsOutlines = 4,
    ShowNoMasks = 5,

    [Obsolete("Use ShowVisibleMasksAsOutlines.")]
    ShowAllMasksAsOutlines = ShowVisibleMasksAsOutlines,
}

public enum CardBackRevealMode
{
    RevealCurrentTargetOnly = 0,
    RevealCurrentSlotOnly = 1,
    RevealAllTargets = 2,
    RevealAllSlots = 3,
}

public enum CardCueMode
{
    HideCurrentSlotOnly = 0,
    HideAllSlotsCueCurrent = 1,
    HideAllSlotsSameStyle = 2,
}

public enum CardContextStatus
{
    Draft = 0,
    Ready = 1,
    Generated = 2,
    Outdated = 3,
    Disabled = 4,
}
