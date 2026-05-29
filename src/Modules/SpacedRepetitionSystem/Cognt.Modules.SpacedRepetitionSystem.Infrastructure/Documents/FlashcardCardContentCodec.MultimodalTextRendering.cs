using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static IReadOnlyList<MultimodalTextSegmentReadModel> BuildClozeAwareTextSegments(
        string text,
        IReadOnlyDictionary<int, CardSlot> slotsByDisplayIndex,
        IReadOnlyList<CardTarget> targets,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool isAnswerVisible)
    {
        IReadOnlyList<FlashcardClozeText.ClozeMarker> markers = FlashcardClozeText.GetMarkers(text);
        if (markers.Count == 0)
        {
            return BuildTargetAwareTextSegments(
                text,
                targets,
                currentSlotId,
                presentation,
                isAnswerVisible);
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

    private static IReadOnlyList<MultimodalTextSegmentReadModel> BuildTargetAwareTextSegments(
        string text,
        IReadOnlyList<CardTarget> targets,
        string currentSlotId,
        CardPresentationPolicy presentation,
        bool isAnswerVisible)
    {
        IReadOnlyList<TargetTextOccurrence> occurrences = FindTargetTextOccurrences(text, targets);
        if (occurrences.Count == 0)
        {
            return [new MultimodalTextSegmentReadModel(text, null, FlashcardSemanticStyle.ResolvedTarget, false)];
        }

        List<MultimodalTextSegmentReadModel> segments = [];
        int position = 0;
        foreach (TargetTextOccurrence occurrence in occurrences)
        {
            if (occurrence.Start < position)
            {
                continue;
            }

            if (occurrence.Start > position)
            {
                segments.Add(new MultimodalTextSegmentReadModel(
                    text[position..occurrence.Start],
                    null,
                    FlashcardSemanticStyle.ResolvedTarget,
                    false));
            }

            string answerText = text.Substring(occurrence.Start, occurrence.Length);
            segments.Add(CreatePolicyAwareClozeTextSegment(
                answerText,
                occurrence.SlotId,
                currentSlotId,
                presentation,
                isAnswerVisible));
            position = occurrence.Start + occurrence.Length;
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

    private static IReadOnlyList<TargetTextOccurrence> FindTargetTextOccurrences(
        string text,
        IReadOnlyList<CardTarget> targets)
    {
        if (string.IsNullOrWhiteSpace(text) || targets.Count == 0)
        {
            return [];
        }

        List<TargetTextOccurrence> candidates = [];
        HashSet<string> seenTargets = new(StringComparer.Ordinal);
        foreach (CardTarget target in targets)
        {
            if (target.IsDisabled
                || string.IsNullOrWhiteSpace(target.SlotId)
                || string.IsNullOrWhiteSpace(target.Answer))
            {
                continue;
            }

            string answer = target.Answer.Trim();
            string targetKey = string.Concat(target.SlotId, "\u001F", answer);
            if (!seenTargets.Add(targetKey))
            {
                continue;
            }

            int start = text.IndexOf(answer, StringComparison.Ordinal);
            while (start >= 0)
            {
                candidates.Add(new TargetTextOccurrence(start, answer.Length, target.SlotId));
                int nextStart = start + Math.Max(1, answer.Length);
                start = nextStart >= text.Length
                    ? -1
                    : text.IndexOf(answer, nextStart, StringComparison.Ordinal);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Start)
            .ThenByDescending(candidate => candidate.Length)
            .ToArray();
    }

    private sealed record TargetTextOccurrence(int Start, int Length, string SlotId);

}
