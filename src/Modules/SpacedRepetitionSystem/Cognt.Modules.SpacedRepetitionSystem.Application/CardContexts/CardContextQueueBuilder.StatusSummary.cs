namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static CardCandidateStatus DetermineStatus(
        CardContext context,
        CardSlot slot,
        CandidateValidation validation,
        GeneratedCardContextCardReadModel? generated,
        string snapshotHash)
    {
        if (context.Status == CardContextStatus.Disabled)
        {
            return CardCandidateStatus.Disabled;
        }

        if (slot.IsDisabled)
        {
            return CardCandidateStatus.Disabled;
        }

        if (!validation.IsValid)
        {
            return validation.IsDraft
                ? CardCandidateStatus.Draft
                : CardCandidateStatus.Invalid;
        }

        if (generated is null)
        {
            return CardCandidateStatus.Ready;
        }

        return string.Equals(generated.SnapshotHash, snapshotHash, StringComparison.Ordinal)
            ? CardCandidateStatus.Generated
            : CardCandidateStatus.Outdated;
    }

    private static NoteToCardQueueSummaryReadModel BuildSummary(IReadOnlyList<CardCandidateReadModel> items)
    {
        return new NoteToCardQueueSummaryReadModel(
            items.Count,
            items.Count(item => item.Status == CardCandidateStatus.Draft),
            items.Count(item => item.Status == CardCandidateStatus.Ready),
            items.Count(item => item.Status == CardCandidateStatus.Invalid),
            items.Count(item => item.Status == CardCandidateStatus.Generated),
            items.Count(item => item.Status == CardCandidateStatus.Outdated),
            items.Count(item => item.Status == CardCandidateStatus.Disabled));
    }
}
