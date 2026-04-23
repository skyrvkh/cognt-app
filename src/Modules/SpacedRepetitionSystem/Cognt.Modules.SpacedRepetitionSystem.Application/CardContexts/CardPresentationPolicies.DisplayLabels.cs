namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardPresentationPolicies
{
    public static string GetDisplayLabel(CardPresentationPreset preset)
    {
        return preset switch
        {
            CardPresentationPreset.TextHideAllCueCurrent => "Hide all clozes, cue current",
            CardPresentationPreset.TextHideAllCueCurrentAndCueCurrentImageMask => "Hide all clozes + cue current image mask",
            CardPresentationPreset.TextHideCurrentShowSiblings => "Hide current cloze, show siblings",
            CardPresentationPreset.TextHideCurrentShowSiblingsAndImageHideAllCueCurrent => "Hide current cloze, show siblings + hide all masks, cue current mask",
            CardPresentationPreset.TextHideAllCueCurrentAndImageHideAllCueCurrent => "Hide all clozes + hide all masks, cue current mask",
            CardPresentationPreset.TextHideCurrentShowSiblingsAndCueCurrentImageMask => "Hide current cloze, show siblings + cue current image mask",
            CardPresentationPreset.Custom => "Custom",
            _ => preset.ToString(),
        };
    }
}
