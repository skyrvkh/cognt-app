namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardSlot CreateNewSlot(int displayClozeIndex)
    {
        if (displayClozeIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayClozeIndex));
        }

        return new CardSlot(NewStableId("slot"), displayClozeIndex, []);
    }

    public static CardContext AddSlot(CardContext context, CardSlot slot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(slot);

        if (FindSlotIndexById(context.Slots, slot.Id) >= 0)
        {
            return context;
        }

        List<CardSlot> slots = [..context.Slots, slot];
        return context with { Slots = slots };
    }

    public static CardContext SetCardSlotDisabled(
        CardContext context,
        string slotId,
        bool isDisabled)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        CardSlot[] slots = context.Slots.ToArray();
        int slotIndex = Array.FindIndex(
            slots,
            slot => string.Equals(slot.Id, slotId, StringComparison.Ordinal));

        if (slotIndex < 0)
        {
            throw new InvalidOperationException($"Card context slot '{slotId}' does not exist.");
        }

        slots[slotIndex] = slots[slotIndex] with { IsDisabled = isDisabled };
        return context with { Slots = slots };
    }

    private static int FindSlotIndexById(IReadOnlyList<CardSlot> slots, string slotId)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (string.Equals(slots[index].Id, slotId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
