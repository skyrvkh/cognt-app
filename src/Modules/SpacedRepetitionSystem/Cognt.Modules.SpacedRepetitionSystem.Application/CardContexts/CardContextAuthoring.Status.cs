namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext UpdateStatusFromSnapshotHash(CardContext context, string currentSnapshotHash)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSnapshotHash);

        if (context.Status == CardContextStatus.Disabled)
        {
            return context;
        }

        CardContextStatus nextStatus = DetermineStatus(context, currentSnapshotHash);
        return context with { Status = nextStatus };
    }

    internal static CardContextStatus DetermineStatus(CardContext context, string currentSnapshotHash)
    {
        bool hasAnySlots = context.Slots.Count > 0
            && context.Targets.Count > 0
            && context.Slots.Any(slot => slot.TargetIds.Count > 0);

        if (!hasAnySlots)
        {
            return CardContextStatus.Draft;
        }

        if (string.IsNullOrWhiteSpace(context.LastGeneratedSnapshotHash))
        {
            return CardContextStatus.Ready;
        }

        return string.Equals(context.LastGeneratedSnapshotHash, currentSnapshotHash, StringComparison.Ordinal)
            ? CardContextStatus.Generated
            : CardContextStatus.Outdated;
    }
}
