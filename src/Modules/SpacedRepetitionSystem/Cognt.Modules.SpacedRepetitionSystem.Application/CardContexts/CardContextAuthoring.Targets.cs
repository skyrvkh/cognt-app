namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardTarget CreateCardTarget(string slotId, string answer, string? hint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);

        return new CardTarget(
            NewStableId("target"),
            slotId,
            NormalizeInlineText(answer),
            string.IsNullOrWhiteSpace(hint) ? null : hint.Trim(),
            [],
            []);
    }

    public static CardContext AddTargetToExistingSlot(
        CardContext context,
        string slotId,
        CardTarget target)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(target);

        int slotIndex = context.Slots
            .ToList()
            .FindIndex(slot => string.Equals(slot.Id, slotId, StringComparison.Ordinal));

        if (slotIndex < 0 || slotIndex >= context.Slots.Count)
        {
            throw new InvalidOperationException($"Card context slot '{slotId}' does not exist.");
        }

        List<CardSlot> slots = [..context.Slots];
        CardSlot currentSlot = slots[slotIndex];
        slots[slotIndex] = currentSlot with
        {
            TargetIds = currentSlot.TargetIds
                .Concat([target.Id])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };

        List<CardTarget> targets = [..context.Targets];
        if (targets.All(existing => !string.Equals(existing.Id, target.Id, StringComparison.Ordinal)))
        {
            targets.Add(target);
        }

        return context with { Slots = slots, Targets = targets };
    }

    public static CardContext UpdateTargetDetails(
        CardContext context,
        string targetId,
        string answer,
        string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);

        List<CardTarget> targets = [..context.Targets];
        int targetIndex = targets.FindIndex(target => string.Equals(target.Id, targetId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            throw new InvalidOperationException($"Card context target '{targetId}' does not exist.");
        }

        CardTarget target = targets[targetIndex];
        targets[targetIndex] = target with
        {
            Answer = NormalizeInlineText(answer),
            Hint = string.IsNullOrWhiteSpace(hint) ? null : hint.Trim(),
        };

        return context with { Targets = targets };
    }

    public static CardContext LinkTextAnchorToTarget(
        CardContext context,
        string targetId,
        TextAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(anchor);

        List<CardTarget> targets = [..context.Targets];
        int targetIndex = targets.FindIndex(target => string.Equals(target.Id, targetId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            throw new InvalidOperationException($"Card context target '{targetId}' does not exist.");
        }

        CardTarget target = targets[targetIndex];
        targets[targetIndex] = target with
        {
            TextAnchors = target.TextAnchors
                .Concat([anchor])
                .DistinctBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray(),
        };

        return context with
        {
            Targets = targets,
            ContextBlockIds = context.ContextBlockIds
                .Concat([anchor.BlockId])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
    }
}
