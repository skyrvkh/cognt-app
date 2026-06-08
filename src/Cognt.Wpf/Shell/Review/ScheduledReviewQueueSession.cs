using System.Diagnostics.CodeAnalysis;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace CogntApp.Shell.Review;

internal sealed class ScheduledReviewQueueSession
{
    private readonly FlashcardReviewQueueReadModel sourceQueue;
    private readonly List<ScheduledReviewQueueItem> availableCards;
    private readonly List<ScheduledReviewQueueItem> pendingCards = [];

    internal ScheduledReviewQueueSession(FlashcardReviewQueueReadModel sourceQueue)
    {
        ArgumentNullException.ThrowIfNull(sourceQueue);
        if (sourceQueue.Mode != FlashcardReviewQueueMode.Scheduled)
        {
            throw new ArgumentException("Only scheduled review queues can be cached.", nameof(sourceQueue));
        }

        this.sourceQueue = sourceQueue;
        availableCards = new List<ScheduledReviewQueueItem>(sourceQueue.Cards.Count);
        foreach (FlashcardReviewCardReadModel card in sourceQueue.Cards)
        {
            availableCards.Add(CreateInitialQueueItem(card));
        }
    }

    internal bool HasKnownCards => availableCards.Count > 0 || pendingCards.Count > 0;

    internal bool RecordReviewedCard(
        RecordedFlashcardReviewReadModel reviewResult,
        DateTimeOffset nowUtc)
    {
        PromoteDueCards(nowUtc);
        int index = availableCards.FindIndex(card => card.Card.Id == reviewResult.CardId);
        if (index < 0)
        {
            return false;
        }

        FlashcardReviewCardReadModel reviewedCard = availableCards[index].Card;
        availableCards.RemoveAt(index);
        ScheduledReviewQueueItem nextItem = CreateReviewedQueueItem(reviewedCard, reviewResult, nowUtc);
        if (IsDue(nextItem, nowUtc))
        {
            InsertAvailable(nextItem, nowUtc);
        }
        else
        {
            InsertPending(nextItem);
        }

        return true;
    }

    internal bool TryCreateReadModel(
        DateTimeOffset nowUtc,
        [NotNullWhen(true)] out FlashcardReviewQueueReadModel? reviewQueue)
    {
        PromoteDueCards(nowUtc);
        if (!HasKnownCards)
        {
            reviewQueue = null;
            return false;
        }

        reviewQueue = CreateReadModelCore(nowUtc);
        return true;
    }

    internal bool TryCreateDueReadModel(
        DateTimeOffset nowUtc,
        [NotNullWhen(true)] out FlashcardReviewQueueReadModel? reviewQueue)
    {
        PromoteDueCards(nowUtc);
        if (availableCards.Count == 0)
        {
            reviewQueue = null;
            return false;
        }

        reviewQueue = CreateReadModelCore(nowUtc);
        return true;
    }

    private FlashcardReviewQueueReadModel CreateReadModelCore(DateTimeOffset nowUtc)
    {
        FlashcardReviewCardReadModel[] cards = availableCards
            .Select(card => ApplyPlacement(card.Card, card.CategoryFor(nowUtc), card.DueUtc))
            .ToArray();
        DateTimeOffset? pendingDueUtc = pendingCards.Count == 0
            ? null
            : pendingCards.Min(card => card.DueUtc);
        DateTimeOffset? nextDueUtc = Earliest(sourceQueue.NextDueUtc, pendingDueUtc);
        DateTimeOffset? nextIntradayDueUtc = Earliest(sourceQueue.NextIntradayDueUtc, pendingDueUtc);

        return sourceQueue with
        {
            Cards = cards,
            Summary = sourceQueue.Summary with
            {
                TotalCards = cards.Length,
            },
            NextDueUtc = nextDueUtc,
            NextIntradayDueUtc = nextIntradayDueUtc,
        };
    }

    private void PromoteDueCards(DateTimeOffset nowUtc)
    {
        for (int index = pendingCards.Count - 1; index >= 0; index--)
        {
            ScheduledReviewQueueItem card = pendingCards[index];
            if (!IsDue(card, nowUtc))
            {
                continue;
            }

            pendingCards.RemoveAt(index);
            InsertAvailable(card, nowUtc);
        }
    }

    private void InsertAvailable(ScheduledReviewQueueItem card, DateTimeOffset nowUtc)
    {
        availableCards.Add(card);
        availableCards.Sort((left, right) => CompareQueuePosition(left, right, nowUtc));
    }

    private void InsertPending(ScheduledReviewQueueItem card)
    {
        int existingIndex = pendingCards.FindIndex(item => item.Card.Id == card.Card.Id);
        if (existingIndex >= 0)
        {
            pendingCards.RemoveAt(existingIndex);
        }

        pendingCards.Add(card);
        pendingCards.Sort(static (left, right) =>
        {
            int dueComparison = left.DueUtc.CompareTo(right.DueUtc);
            return dueComparison != 0
                ? dueComparison
                : left.Card.Id.CompareTo(right.Card.Id);
        });
    }

    private static ScheduledReviewQueueItem CreateInitialQueueItem(FlashcardReviewCardReadModel card)
    {
        FlashcardReviewQueueCategory category = card.QueueCategory ?? FlashcardReviewQueueCategory.New;
        DateTimeOffset dueUtc = card.QueueDueUtc ?? DateTimeOffset.MinValue;
        return new ScheduledReviewQueueItem(card, null, category, dueUtc);
    }

    private static ScheduledReviewQueueItem CreateReviewedQueueItem(
        FlashcardReviewCardReadModel card,
        RecordedFlashcardReviewReadModel reviewResult,
        DateTimeOffset nowUtc)
    {
        FlashcardReviewQueueCategory category = ToQueueCategory(reviewResult.State, reviewResult.DueUtc, nowUtc);
        return new ScheduledReviewQueueItem(
            ApplyPlacement(card, category, reviewResult.DueUtc),
            reviewResult.State,
            category,
            reviewResult.DueUtc);
    }

    private static FlashcardReviewQueueCategory ToQueueCategory(
        FlashcardCardStateReadModel state,
        DateTimeOffset dueUtc,
        DateTimeOffset nowUtc)
    {
        return state switch
        {
            FlashcardCardStateReadModel.Learning => FlashcardReviewQueueCategory.Learning,
            FlashcardCardStateReadModel.Relearning => FlashcardReviewQueueCategory.Relearning,
            FlashcardCardStateReadModel.Review => IsOverdue(dueUtc, nowUtc)
                ? FlashcardReviewQueueCategory.Overdue
                : FlashcardReviewQueueCategory.Due,
            _ => FlashcardReviewQueueCategory.New,
        };
    }

    private static FlashcardReviewCardReadModel ApplyPlacement(
        FlashcardReviewCardReadModel card,
        FlashcardReviewQueueCategory category,
        DateTimeOffset dueUtc)
    {
        return card with
        {
            QueueCategory = category,
            QueueDueUtc = dueUtc,
        };
    }

    private static int CompareQueuePosition(
        ScheduledReviewQueueItem left,
        ScheduledReviewQueueItem right,
        DateTimeOffset nowUtc)
    {
        int categoryComparison = left.CategoryFor(nowUtc).CompareTo(right.CategoryFor(nowUtc));
        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        int dueComparison = left.DueUtc.CompareTo(right.DueUtc);
        return dueComparison != 0
            ? dueComparison
            : left.Card.Id.CompareTo(right.Card.Id);
    }

    private static bool IsDue(ScheduledReviewQueueItem card, DateTimeOffset nowUtc)
    {
        return card.DueUtc <= nowUtc;
    }

    private static bool IsOverdue(DateTimeOffset dueUtc, DateTimeOffset nowUtc)
    {
        return dueUtc.ToLocalTime().Date < nowUtc.ToLocalTime().Date;
    }

    private static DateTimeOffset? Earliest(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left.HasValue && right.HasValue)
        {
            return left.Value <= right.Value ? left : right;
        }

        return left ?? right;
    }

    private sealed record ScheduledReviewQueueItem(
        FlashcardReviewCardReadModel Card,
        FlashcardCardStateReadModel? State,
        FlashcardReviewQueueCategory Category,
        DateTimeOffset DueUtc)
    {
        internal FlashcardReviewQueueCategory CategoryFor(DateTimeOffset nowUtc)
        {
            return State == FlashcardCardStateReadModel.Review && IsOverdue(DueUtc, nowUtc)
                ? FlashcardReviewQueueCategory.Overdue
                : Category;
        }
    }
}
