namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static partial class CardContextCardExpansion
{
    internal static IReadOnlyList<ExpandedCardContextCard> Expand(CardContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Dictionary<string, CardTarget> targetsById = CreateTargetLookup(snapshot.Targets);

        List<ExpandedCardContextCard> cards = [];
        foreach (CardSlot slot in snapshot.Slots
                     .OrderBy(slot => slot.DisplayClozeIndex)
                     .ThenBy(slot => slot.Id, StringComparer.Ordinal))
        {
            if (slot.IsDisabled)
            {
                continue;
            }

            IReadOnlyList<CardTarget> targets = ResolveTargets(slot, targetsById, snapshot.Targets);
            if (targets.Count == 0)
            {
                continue;
            }

            string snapshotHash = CardContextSnapshotFactory.ComputeSlotSnapshotHash(snapshot, slot.Id);
            CardPresentationPolicy presentation = CardPresentationPolicies.Resolve(snapshot, slot);
            cards.Add(new ExpandedCardContextCard(
                snapshot.SourceRelPath,
                snapshot.Id,
                snapshot.ContextContent,
                snapshot.Slots,
                snapshot.Targets,
                snapshot.Images,
                presentation,
                snapshot.Status,
                snapshotHash,
                slot.Id,
                CreateTargetIdArray(targets)));
        }

        return cards;
    }

}

internal sealed record ExpandedCardContextCard(
    string SourceRelPath,
    string ContextId,
    string ContextContent,
    IReadOnlyList<CardSlot> Slots,
    IReadOnlyList<CardTarget> Targets,
    IReadOnlyList<CardContextImageSurface> Images,
    CardPresentationPolicy Presentation,
    CardContextStatus Status,
    string SnapshotHash,
    string CurrentSlotId,
    IReadOnlyList<string> CurrentTargetIds);
