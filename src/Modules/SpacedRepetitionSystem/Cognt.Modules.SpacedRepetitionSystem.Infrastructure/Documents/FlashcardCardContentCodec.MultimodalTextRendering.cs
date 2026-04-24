using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static IReadOnlyList<MultimodalTextSegmentReadModel> BuildClozeAwareTextSegments(
        string text,
        IReadOnlyDictionary<int, CardSlot> slotsByDisplayIndex,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool isAnswerVisible)
    {
        IReadOnlyList<FlashcardClozeText.ClozeMarker> markers = FlashcardClozeText.GetMarkers(text);
        if (markers.Count == 0)
        {
            return [new MultimodalTextSegmentReadModel(text, null, FlashcardSemanticStyle.ResolvedTarget, false)];
        }

        List<MultimodalTextSegmentReadModel> segments = [];
        int position = 0;
        foreach (FlashcardClozeText.ClozeMarker marker in markers)
        {
            if (marker.Start > position)
            {
                segments.Add(new MultimodalTextSegmentReadModel(
                    text[position..marker.Start],
                    null,
                    FlashcardSemanticStyle.ResolvedTarget,
                    false));
            }

            if (!slotsByDisplayIndex.TryGetValue(marker.Index, out CardSlot? slot))
            {
                segments.Add(new MultimodalTextSegmentReadModel(
                    marker.Text,
                    null,
                    FlashcardSemanticStyle.ResolvedTarget,
                    false));
            }
            else
            {
                segments.Add(CreatePolicyAwareClozeTextSegment(marker.Text, slot.Id, currentSlotId, presentation, isAnswerVisible));
            }

            position = marker.Start + marker.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new MultimodalTextSegmentReadModel(
                text[position..],
                null,
                FlashcardSemanticStyle.ResolvedTarget,
                false));
        }

        return segments;
    }

}
