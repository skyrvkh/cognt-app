namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardPresentationPolicies
{
    internal static string ToStableKey(CardPresentationPolicy policy)
    {
        return string.Join(
            ":",
            policy.Preset,
            policy.TextFrontMode,
            policy.ImageFrontMode,
            policy.BackRevealMode,
            policy.CueCurrentTarget,
            policy.RevealSiblingAnswersOnBack);
    }
}
