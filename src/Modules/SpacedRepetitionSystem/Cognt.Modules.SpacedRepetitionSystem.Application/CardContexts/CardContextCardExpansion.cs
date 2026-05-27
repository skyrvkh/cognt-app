namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static class CardContextCardExpansion
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

    private static Dictionary<string, CardTarget> CreateTargetLookup(IReadOnlyList<CardTarget> targets)
    {
        Dictionary<string, CardTarget> targetsById = new(targets.Count, StringComparer.Ordinal);
        foreach (CardTarget target in targets)
        {
            targetsById.Add(target.Id, target);
        }

        return targetsById;
    }

    private static IReadOnlyList<CardTarget> ResolveTargets(
        CardSlot slot,
        IReadOnlyDictionary<string, CardTarget> targetsById,
        IReadOnlyList<CardTarget> allTargets)
    {
        if (slot.TargetIds.Count > 0)
        {
            List<CardTarget> resolvedTargets = new(slot.TargetIds.Count);
            foreach (string targetId in slot.TargetIds)
            {
                if (targetsById.TryGetValue(targetId, out CardTarget? target) && !target.IsDisabled)
                {
                    resolvedTargets.Add(target);
                }
            }

            return resolvedTargets.ToArray();
        }

        List<CardTarget> slotTargets = new(allTargets.Count);
        foreach (CardTarget target in allTargets)
        {
            if (!target.IsDisabled && string.Equals(target.SlotId, slot.Id, StringComparison.Ordinal))
            {
                slotTargets.Add(target);
            }
        }

        return slotTargets.ToArray();
    }

    private static string[] CreateTargetIdArray(IReadOnlyList<CardTarget> targets)
    {
        string[] targetIds = new string[targets.Count];
        for (int index = 0; index < targets.Count; index++)
        {
            targetIds[index] = targets[index].Id;
        }

        return targetIds;
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
