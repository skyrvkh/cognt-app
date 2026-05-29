using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private static int? ResolveEditedClozeIndex(string content, int? fallbackIndex)
    {
        IReadOnlyList<FlashcardClozeText.ClozeMarker> markers = FlashcardClozeText.GetMarkers(
            BlockSuiteDocumentText.ToPlainText(content));
        return markers.Count == 0
            ? fallbackIndex
            : markers[^1].Index;
    }

    private static FlashcardCardDocument WithSynchronizedTextClozeSlots(
        this FlashcardCardDocument document,
        string content)
    {
        IReadOnlyList<FlashcardClozeText.ClozeMarker> markers = FlashcardClozeText.GetMarkers(
            BlockSuiteDocumentText.ToPlainText(content));
        if (markers.Count == 0)
        {
            return document;
        }

        IReadOnlyList<CardSlot> sourceSlots = document.Slots ?? document.Groups ?? [];
        IReadOnlyList<CardTarget> sourceTargets = document.Targets ?? [];
        List<CardSlot> slots = [..sourceSlots];
        List<CardTarget> targets = [..sourceTargets];
        Dictionary<int, CardSlot> slotsByDisplayIndex = CreateEditableSlotsByDisplayIndex(slots);
        foreach (FlashcardClozeText.ClozeMarker marker in markers)
        {
            if (slotsByDisplayIndex.ContainsKey(marker.Index))
            {
                continue;
            }

            CardSlot slot = CreateTextClozeSlot(marker, slots, targets);
            slotsByDisplayIndex[marker.Index] = slot;
        }

        CardSlot activeSlot = slotsByDisplayIndex[markers[^1].Index];

        FlashcardCardSourceDocument? source = document.Source is null
            ? null
            : document.Source with
            {
                SlotId = activeSlot.Id,
                TargetIds = activeSlot.TargetIds,
            };

        return document with
        {
            Slots = slots
                .OrderBy(slot => slot.DisplayClozeIndex)
                .ToArray(),
            Targets = targets,
            CurrentSlotId = activeSlot.Id,
            Source = source,
        };
    }

    private static Dictionary<int, CardSlot> CreateEditableSlotsByDisplayIndex(IReadOnlyList<CardSlot> slots)
    {
        Dictionary<int, CardSlot> slotsByDisplayIndex = [];
        foreach (CardSlot slot in slots)
        {
            if (slot.DisplayClozeIndex > 0 && !slot.IsDisabled)
            {
                slotsByDisplayIndex.TryAdd(slot.DisplayClozeIndex, slot);
            }
        }

        return slotsByDisplayIndex;
    }

    private static CardSlot CreateTextClozeSlot(
        FlashcardClozeText.ClozeMarker marker,
        List<CardSlot> slots,
        List<CardTarget> targets)
    {
        CardSlot slot = CardContextAuthoring.CreateNewSlot(marker.Index);
        string answer = string.IsNullOrWhiteSpace(marker.Text) ? "Cloze" : marker.Text;
        CardTarget target = CardContextAuthoring.CreateCardTarget(slot.Id, answer);
        slot = slot with
        {
            TargetIds = [target.Id],
            Order = slots.Count,
        };
        slots.Add(slot);
        targets.Add(target);
        return slot;
    }
}
