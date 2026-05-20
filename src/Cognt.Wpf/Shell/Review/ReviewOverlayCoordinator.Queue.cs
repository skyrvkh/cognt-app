using System.Windows.Threading;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.Views;

namespace CogntApp.Shell.Review;

internal sealed partial class ReviewOverlayCoordinator
{
    private static readonly TimeSpan MaximumAutoResumeDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinimumQueueRefreshDelay = TimeSpan.FromSeconds(1);

    private DispatcherTimer? queueRefreshTimer;

    private Task ShowReviewQueueAsync(FlashcardReviewQueueReadModel reviewQueue, bool exitWhenUnavailable)
    {
        ReviewView review = reviewView
            ?? throw new InvalidOperationException("The review surface is not ready.");

        StopQueueRefreshTimer();
        if (reviewQueue.Cards.Count > 0)
        {
            review.ShowCard(reviewQueue.Cards[0], reviewQueue.Summary);
            return Task.CompletedTask;
        }

        if (reviewQueue.Mode == FlashcardReviewQueueMode.Cram)
        {
            if (exitWhenUnavailable)
            {
                ExitReviewRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("No cards match the current cram scope.");
        }

        DateTimeOffset? nextDueUtc = GetPreferredNextDueUtc(reviewQueue);
        if (CanAutoResume(reviewQueue) && nextDueUtc.HasValue)
        {
            review.ShowWaitingState(reviewQueue.Summary, nextDueUtc);
            ScheduleQueueRefresh(nextDueUtc.Value);
            return Task.CompletedTask;
        }

        if (exitWhenUnavailable)
        {
            ExitReviewRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("No review cards are available for the current daily limits.");
    }

    private FlashcardReviewQueueReadModel LoadReviewQueue(string collectionId)
    {
        return reviewQueueQuery.Execute(new FlashcardReviewQueueQueryRequest(
            collectionId,
            activeQueueMode,
            activeQueueMode == FlashcardReviewQueueMode.Cram ? activeCramScope : null));
    }

    private async Task ReloadReviewQueueAsync(string collectionId, bool exitWhenUnavailable)
    {
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);
        await ShowReviewQueueAsync(reviewQueue, exitWhenUnavailable);
    }

    private Task ShowMaterializedCramQueueAsync(bool exitWhenUnavailable)
    {
        ReviewView review = reviewView
            ?? throw new InvalidOperationException("The review surface is not ready.");
        CramSessionState state = activeCramState
            ?? throw new InvalidOperationException("No active cram session exists.");
        FlashcardReviewQueueSummaryReadModel summary = activeCramSummary
            ?? new FlashcardReviewQueueSummaryReadModel(0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (state.IsEmpty)
        {
            review.ShowCramEmptyState(summary);
            return Task.CompletedTask;
        }

        if (state.IsComplete)
        {
            review.ShowCramCompletion(state.CreateCompletionSummary(DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        }

        if (!state.CurrentCardId.HasValue
            || !activeCramCardsById.TryGetValue(state.CurrentCardId.Value, out FlashcardReviewCardReadModel? card))
        {
            if (exitWhenUnavailable)
            {
                ExitReviewRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("The next cram card is no longer available.");
        }

        review.ShowCard(
            card,
            summary with
            {
                TotalCards = state.ValidCardCount,
            });
        review.ShowCramProgress(ToCramProgress(state));
        return Task.CompletedTask;
    }

    private static CramReviewProgress ToCramProgress(CramSessionState state)
    {
        CramCardProgress progress = state.CurrentProgress
            ?? throw new InvalidOperationException("No active cram progress exists.");
        int effectivePasses = progress.EffectivePasses(state.Settings.PassCountingMode);
        string progressLabel = state.Settings.PassCountingMode == CramPassCountingMode.Consecutive
            ? "Current streak"
            : "Successful ratings";

        return new CramReviewProgress(
            state.MasteredCardCount,
            state.ValidCardCount,
            state.RemainingCardCount,
            progressLabel,
            effectivePasses,
            state.Settings.RequiredPassCount,
            progress.TotalAttempts);
    }

    private void ScheduleQueueRefresh(DateTimeOffset dueUtc)
    {
        TimeSpan delay = dueUtc - DateTimeOffset.UtcNow;
        if (delay < MinimumQueueRefreshDelay)
        {
            delay = MinimumQueueRefreshDelay;
        }

        StopQueueRefreshTimer();
        queueRefreshTimer = new DispatcherTimer
        {
            Interval = delay,
        };
        queueRefreshTimer.Tick += QueueRefreshTimer_Tick;
        queueRefreshTimer.Start();
    }

    private void StopQueueRefreshTimer()
    {
        if (queueRefreshTimer is null)
        {
            return;
        }

        queueRefreshTimer.Stop();
        queueRefreshTimer.Tick -= QueueRefreshTimer_Tick;
        queueRefreshTimer = null;
    }

    private async void QueueRefreshTimer_Tick(object? sender, EventArgs e)
    {
        StopQueueRefreshTimer();
        if (reviewView is null || string.IsNullOrWhiteSpace(activeCollectionId))
        {
            return;
        }

        try
        {
            await ReloadReviewQueueAsync(activeCollectionId, exitWhenUnavailable: true);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Review", $"The practice queue could not be refreshed: {ex.Message}");
            ExitReviewRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void EnsureReviewQueueAvailable(FlashcardReviewQueueReadModel reviewQueue)
    {
        if (reviewQueue.Mode == FlashcardReviewQueueMode.Cram)
        {
            if (reviewQueue.Cards.Count == 0)
            {
                throw new InvalidOperationException("No cards match the current cram scope.");
            }

            return;
        }

        if (reviewQueue.Cards.Count == 0 && !CanAutoResume(reviewQueue))
        {
            throw new InvalidOperationException("No review cards are available for the current daily limits.");
        }
    }

    private static bool CanAutoResume(FlashcardReviewQueueReadModel reviewQueue)
    {
        DateTimeOffset? nextDueUtc = GetPreferredNextDueUtc(reviewQueue);
        if (!nextDueUtc.HasValue)
        {
            return false;
        }

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        return nextDueUtc.Value > nowUtc
            && nextDueUtc.Value - nowUtc <= MaximumAutoResumeDelay;
    }

    private static DateTimeOffset? GetPreferredNextDueUtc(FlashcardReviewQueueReadModel reviewQueue)
    {
        if (reviewQueue.NextDueUtc.HasValue && reviewQueue.NextIntradayDueUtc.HasValue)
        {
            return reviewQueue.NextDueUtc.Value <= reviewQueue.NextIntradayDueUtc.Value
                ? reviewQueue.NextDueUtc
                : reviewQueue.NextIntradayDueUtc;
        }

        return reviewQueue.NextIntradayDueUtc ?? reviewQueue.NextDueUtc;
    }
}
