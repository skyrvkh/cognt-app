using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static partial class NoteToCardPresentationPolicyApplicator
{
    private static CardContext ApplyToSelectedSlot(
        CardContext context,
        CardCandidateReadModel selected,
        CardPresentationPolicy policy)
    {
        if (!string.Equals(context.Id, selected.ContextId, StringComparison.Ordinal))
        {
            return context;
        }

        bool changedSelectedSlot = false;
        CardSlot[] selectedSlots = context.Slots
            .Select(slot =>
            {
                if (!string.Equals(slot.Id, selected.SlotId, StringComparison.Ordinal))
                {
                    return slot;
                }

                changedSelectedSlot = !Equals(slot.PresentationOverride ?? context.DefaultPresentationPolicy, policy)
                    || !Equals(slot.PresentationOverride, policy);
                return slot with { PresentationOverride = policy };
            })
            .ToArray();

        return changedSelectedSlot
            ? context with { Slots = selectedSlots }
            : context;
    }
}
