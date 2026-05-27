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

        int slotIndex = FindSlotIndexById(context.Slots, slotId);

        if (slotIndex < 0 || slotIndex >= context.Slots.Count)
        {
            throw new InvalidOperationException($"Card context slot '{slotId}' does not exist.");
        }

        List<CardSlot> slots = [..context.Slots];
        CardSlot currentSlot = slots[slotIndex];
        slots[slotIndex] = currentSlot with
        {
            TargetIds = AppendDistinctOrdinal(currentSlot.TargetIds, target.Id),
        };

        List<CardTarget> targets = [..context.Targets];
        if (!ContainsTargetId(targets, target.Id))
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
            TextAnchors = AppendTextAnchorById(target.TextAnchors, anchor),
        };

        return context with
        {
            Targets = targets,
            ContextBlockIds = AppendDistinctOrdinal(context.ContextBlockIds, anchor.BlockId),
        };
    }

    private static bool ContainsTargetId(IReadOnlyList<CardTarget> targets, string targetId)
    {
        foreach (CardTarget target in targets)
        {
            if (string.Equals(target.Id, targetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static TextAnchor[] AppendTextAnchorById(
        IReadOnlyList<TextAnchor> anchors,
        TextAnchor anchor)
    {
        HashSet<string> seenAnchorIds = new(StringComparer.Ordinal);
        TextAnchor[] nextAnchors = new TextAnchor[anchors.Count + 1];
        int count = 0;

        foreach (TextAnchor existing in anchors)
        {
            if (seenAnchorIds.Add(existing.Id))
            {
                nextAnchors[count++] = existing;
            }
        }

        if (seenAnchorIds.Add(anchor.Id))
        {
            nextAnchors[count++] = anchor;
        }

        if (count != nextAnchors.Length)
        {
            Array.Resize(ref nextAnchors, count);
        }

        return nextAnchors;
    }
}
