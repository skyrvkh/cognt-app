using System.Diagnostics.CodeAnalysis;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace CogntApp.Shell.Review;

internal sealed class ReviewOverlayQueueState
{
    private ScheduledReviewQueueSession? scheduledQueue;

    internal string? ActiveCollectionId { get; private set; }

    internal FlashcardReviewQueueMode ActiveQueueMode { get; private set; } = FlashcardReviewQueueMode.Scheduled;

    internal CramScope ActiveCramScope { get; private set; } = CramScope.All;

    internal void BeginScheduled(string collectionId)
    {
        ActiveCollectionId = collectionId;
        ActiveQueueMode = FlashcardReviewQueueMode.Scheduled;
        ActiveCramScope = CramScope.All;
        scheduledQueue = null;
    }

    internal void BeginCram(string collectionId, CramScope scope)
    {
        ActiveCollectionId = collectionId;
        ActiveQueueMode = FlashcardReviewQueueMode.Cram;
        ActiveCramScope = scope.Normalize();
        scheduledQueue = null;
    }

    internal void Reset()
    {
        ActiveCollectionId = null;
        ActiveQueueMode = FlashcardReviewQueueMode.Scheduled;
        ActiveCramScope = CramScope.All;
        scheduledQueue = null;
    }

    internal FlashcardReviewQueueQueryRequest CreateQueueRequest(string collectionId)
    {
        return new FlashcardReviewQueueQueryRequest(
            collectionId,
            ActiveQueueMode,
            ActiveQueueMode == FlashcardReviewQueueMode.Cram ? ActiveCramScope : null);
    }

    internal void CacheScheduledQueue(FlashcardReviewQueueReadModel reviewQueue)
    {
        scheduledQueue = reviewQueue.Mode == FlashcardReviewQueueMode.Scheduled
            ? new ScheduledReviewQueueSession(reviewQueue)
            : null;
    }

    internal bool TryAdvanceScheduledQueue(
        RecordedFlashcardReviewReadModel reviewResult,
        DateTimeOffset nowUtc,
        [NotNullWhen(true)] out FlashcardReviewQueueReadModel? reviewQueue)
    {
        reviewQueue = null;
        if (ActiveQueueMode != FlashcardReviewQueueMode.Scheduled
            || scheduledQueue is null
            || !scheduledQueue.RecordReviewedCard(reviewResult, nowUtc)
            || !scheduledQueue.TryCreateReadModel(nowUtc, out reviewQueue))
        {
            return false;
        }

        return true;
    }

    internal bool TryCreateDueScheduledQueue(
        DateTimeOffset nowUtc,
        [NotNullWhen(true)] out FlashcardReviewQueueReadModel? reviewQueue)
    {
        reviewQueue = null;
        return ActiveQueueMode == FlashcardReviewQueueMode.Scheduled
            && scheduledQueue is not null
            && scheduledQueue.TryCreateDueReadModel(nowUtc, out reviewQueue);
    }
}
