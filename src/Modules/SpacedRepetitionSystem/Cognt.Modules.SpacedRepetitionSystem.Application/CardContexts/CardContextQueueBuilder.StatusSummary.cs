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
        int draftCount = 0;
        int readyCount = 0;
        int invalidCount = 0;
        int generatedCount = 0;
        int outdatedCount = 0;
        int disabledCount = 0;

        foreach (CardCandidateReadModel item in items)
        {
            switch (item.Status)
            {
                case CardCandidateStatus.Draft:
                    draftCount++;
                    break;
                case CardCandidateStatus.Ready:
                    readyCount++;
                    break;
                case CardCandidateStatus.Invalid:
                    invalidCount++;
                    break;
                case CardCandidateStatus.Generated:
                    generatedCount++;
                    break;
                case CardCandidateStatus.Outdated:
                    outdatedCount++;
                    break;
                case CardCandidateStatus.Disabled:
                    disabledCount++;
                    break;
            }
        }

        return new NoteToCardQueueSummaryReadModel(
            items.Count,
            draftCount,
            readyCount,
            invalidCount,
            generatedCount,
            outdatedCount,
            disabledCount);
    }
}
