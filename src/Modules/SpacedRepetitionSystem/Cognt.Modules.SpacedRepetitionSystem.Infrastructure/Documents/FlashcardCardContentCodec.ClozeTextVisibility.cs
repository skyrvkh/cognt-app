using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static MultimodalTextSegmentReadModel CreatePolicyAwareClozeTextSegment(
        string answerText,
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool isAnswerVisible)
    {
        if (isAnswerVisible)
        {
            return CreateBackSideClozeTextSegment(answerText, slotId, currentSlotId, presentation);
        }

        return CreateFrontSideClozeTextSegment(answerText, slotId, currentSlotId, presentation);
    }

    private static MultimodalTextSegmentReadModel CreateBackSideClozeTextSegment(
        string answerText,
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        if (ShouldRevealSlotOnBack(slotId, currentSlotId, presentation)
            || presentation.TextFrontMode is TextClozeFrontMode.HideCurrentShowSiblings
                or TextClozeFrontMode.None
                or TextClozeFrontMode.ShowAll)
        {
            return new MultimodalTextSegmentReadModel(
                answerText,
                slotId,
                FlashcardSemanticStyle.ResolvedTarget,
                false);
        }

        return new MultimodalTextSegmentReadModel(
            HiddenClozeText,
            slotId,
            FlashcardSemanticStyle.HiddenTarget,
            true);
    }

    private static MultimodalTextSegmentReadModel CreateFrontSideClozeTextSegment(
        string answerText,
        string slotId,
        string currentSlotId,
        CardPresentationPolicy presentation)
    {
        bool isCurrentSlot = string.Equals(slotId, currentSlotId, StringComparison.Ordinal);
        return presentation.TextFrontMode switch
        {
            TextClozeFrontMode.None or TextClozeFrontMode.ShowAll => new MultimodalTextSegmentReadModel(
                answerText,
                slotId,
                FlashcardSemanticStyle.ResolvedTarget,
                false),
            TextClozeFrontMode.HideCurrentShowSiblings when isCurrentSlot => new MultimodalTextSegmentReadModel(
                HiddenClozeText,
                slotId,
                presentation.CueCurrentTarget ? FlashcardSemanticStyle.CurrentTarget : FlashcardSemanticStyle.HiddenTarget,
                true),
            TextClozeFrontMode.HideCurrentShowSiblings => new MultimodalTextSegmentReadModel(
                answerText,
                slotId,
                FlashcardSemanticStyle.ResolvedTarget,
                false),
            TextClozeFrontMode.HideAllCueCurrent => new MultimodalTextSegmentReadModel(
                HiddenClozeText,
                slotId,
                ResolveHiddenTargetStyle(isCurrentSlot, presentation.CueCurrentTarget),
                true),
            TextClozeFrontMode.HideAllSameStyle => new MultimodalTextSegmentReadModel(
                HiddenClozeText,
                slotId,
                FlashcardSemanticStyle.HiddenTarget,
                true),
            _ => new MultimodalTextSegmentReadModel(
                HiddenClozeText,
                slotId,
                FlashcardSemanticStyle.HiddenTarget,
                true),
        };
    }
}
