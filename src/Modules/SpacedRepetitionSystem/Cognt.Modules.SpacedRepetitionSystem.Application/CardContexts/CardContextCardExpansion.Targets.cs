namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static partial class CardContextCardExpansion
{
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
