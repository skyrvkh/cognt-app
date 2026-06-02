using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static partial class NoteToCardPresentationPolicyResolver
{
    internal static CardPresentationPolicy? ResolveSharedPolicy(
        CardContextDraftNoteDocument document,
        IReadOnlyList<CardCandidateReadModel> targets)
    {
        if (targets.Count == 0)
        {
            return null;
        }

        Dictionary<string, CardContext> contextsById = CreateCardContextLookup(document.Contexts);
        Dictionary<string, Dictionary<string, CardSlot>> slotLookupsByContextId = new(StringComparer.Ordinal);
        CardPresentationPolicy? sharedPolicy = null;

        foreach (CardCandidateReadModel candidate in targets)
        {
            if (!contextsById.TryGetValue(candidate.ContextId, out CardContext? context))
            {
                return null;
            }

            if (!slotLookupsByContextId.TryGetValue(context.Id, out Dictionary<string, CardSlot>? slotsById))
            {
                slotsById = CreateCardSlotLookup(context.Slots);
                slotLookupsByContextId.Add(context.Id, slotsById);
            }

            slotsById.TryGetValue(candidate.SlotId, out CardSlot? slot);
            CardPresentationPolicy policy = CardPresentationPolicies.Resolve(context, slot);

            if (sharedPolicy is null)
            {
                sharedPolicy = policy;
                continue;
            }

            if (!Equals(sharedPolicy, policy))
            {
                return null;
            }
        }

        return sharedPolicy;
    }

}
