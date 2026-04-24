using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static bool ShouldRevealSlotOnBack(
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        if (presentation.RevealSiblingAnswersOnBack
            || presentation.BackRevealMode is CardBackRevealMode.RevealAllTargets or CardBackRevealMode.RevealAllSlots)
        {
            return true;
        }

        return string.Equals(slotId, currentSlotId, StringComparison.Ordinal);
    }

    private static FlashcardSemanticStyle ResolveHiddenTargetStyle(bool isCurrentSlot, bool cueCurrentTarget)
    {
        if (isCurrentSlot)
        {
            return cueCurrentTarget ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.HiddenTarget;
        }

        return cueCurrentTarget ? FlashcardSemanticStyle.SiblingTarget : FlashcardSemanticStyle.HiddenTarget;
    }
}
