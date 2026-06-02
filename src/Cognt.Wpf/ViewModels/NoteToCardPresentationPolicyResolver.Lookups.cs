using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static partial class NoteToCardPresentationPolicyResolver
{
    private static Dictionary<string, CardContext> CreateCardContextLookup(IReadOnlyList<CardContext> contexts)
    {
        Dictionary<string, CardContext> lookup = new(contexts.Count, StringComparer.Ordinal);
        foreach (CardContext context in contexts)
        {
            lookup[context.Id] = context;
        }

        return lookup;
    }

    private static Dictionary<string, CardSlot> CreateCardSlotLookup(IReadOnlyList<CardSlot> slots)
    {
        Dictionary<string, CardSlot> lookup = new(slots.Count, StringComparer.Ordinal);
        foreach (CardSlot slot in slots)
        {
            lookup[slot.Id] = slot;
        }

        return lookup;
    }
}
