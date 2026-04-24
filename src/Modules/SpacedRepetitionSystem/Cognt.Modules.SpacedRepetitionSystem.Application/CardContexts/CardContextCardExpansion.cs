namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static class CardContextCardExpansion
{
    internal static IReadOnlyList<ExpandedCardContextCard> Expand(CardContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Dictionary<string, CardTarget> targetsById = snapshot.Targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

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
                targets.Select(target => target.Id).ToArray()));
        }

        return cards;
    }

    private static IReadOnlyList<CardTarget> ResolveTargets(
        CardSlot slot,
        IReadOnlyDictionary<string, CardTarget> targetsById,
        IReadOnlyList<CardTarget> allTargets)
    {
        if (slot.TargetIds.Count > 0)
        {
            return slot.TargetIds
                .Select(targetId => targetsById.TryGetValue(targetId, out CardTarget? target) ? target : null)
                .Where(target => target is not null)
                .Select(target => target!)
                .Where(target => !target.IsDisabled)
                .ToArray();
        }

        return allTargets
            .Where(target => string.Equals(target.SlotId, slot.Id, StringComparison.Ordinal))
            .Where(target => !target.IsDisabled)
            .ToArray();
    }
}

public sealed record ExpandedCardContextCard(
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
