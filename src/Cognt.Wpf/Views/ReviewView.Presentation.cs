using System.Windows;
using System.Windows.Controls;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace CogntApp.Views;

public partial class ReviewView
{
    private void UpdateCardState()
    {
        bool hasCard = currentCard is not null;
        CardSideLabel.Text = isWaitingForDueCard
            ? "UP NEXT"
            : isAnswerVisible
                ? "BACK"
                : "FRONT";
        RevealPromptPanel.Visibility = ResponsiveLayout.VisibleWhen(hasCard && !isWaitingForDueCard && !isAnswerVisible);
        AnswerStatePanel.Visibility = ResponsiveLayout.VisibleWhen(hasCard && !isWaitingForDueCard && isAnswerVisible);
        WaitingStatePanel.Visibility = ResponsiveLayout.VisibleWhen(isWaitingForDueCard);
        RaiseReviewCommandStateChanged();
    }

    private bool CanRevealAnswer => currentCard is not null
        && !isAnswerVisible
        && !isRatingInProgress;

    private bool CanRateCurrentCard => currentCard is not null
        && isAnswerVisible
        && !isRatingInProgress
        && !isWaitingForDueCard;

    private void UpdateQueueSummary()
    {
        FlashcardReviewQueueSummaryReadModel summary = queueSummary
            ?? new FlashcardReviewQueueSummaryReadModel(0, 0, 0, 0, 0, 0, 0, 0, 0);

        QueueLearningText.Text = $"Learning {summary.LearningDueCards}";
        QueueRelearningText.Text = $"Relearning {summary.RelearningDueCards}";
        QueueOverdueText.Text = $"Overdue {summary.OverdueCards}";
        QueueDueText.Text = $"Due {summary.DueCards}";
        QueueFutureText.Text = $"Future {summary.FutureReviewCards}";
        QueueNewText.Text = $"New {summary.NewCards}";
        QueueSuspendedText.Text = $"Suspended skipped {summary.SuspendedCardsExcluded}";
        QueueLimitedText.Text = GetLimitSummaryText(summary);
        QueueTotalText.Text = $"Session {summary.TotalCards}";
        QueueRelearningText.Visibility = ResponsiveLayout.VisibleWhen(summary.RelearningDueCards > 0);
        QueueOverdueText.Visibility = ResponsiveLayout.VisibleWhen(summary.OverdueCards > 0);
        QueueFutureText.Visibility = ResponsiveLayout.VisibleWhen(summary.FutureReviewCards > 0);
        QueueSuspendedText.Visibility = ResponsiveLayout.VisibleWhen(summary.SuspendedCardsExcluded > 0);
        QueueLimitedText.Visibility = ResponsiveLayout.VisibleWhen(
            summary.ReviewLimitExcluded + summary.NewLimitExcluded > 0);
    }

    internal void ShowCramProgress(CramReviewProgress progress)
    {
        CramMasteredText.Text = $"Mastered {progress.MasteredCards} / {progress.ValidCards}";
        CramRemainingText.Text = $"Remaining {progress.RemainingCards}";
        CramCurrentProgressText.Text = $"{progress.CurrentProgressLabel} {progress.CurrentPasses} / {progress.RequiredPassCount}";
        CramAttemptsText.Text = $"Attempts on current card {progress.CurrentCardAttempts}";
        SetCramProgressVisibility(true);
        ExitReviewLabel.Text = "Stop Cramming";
    }

    private void SetCramProgressVisibility(bool isVisible)
    {
        Visibility visibility = ResponsiveLayout.VisibleWhen(isVisible);
        CramMasteredText.Visibility = visibility;
        CramRemainingText.Visibility = visibility;
        CramCurrentProgressText.Visibility = visibility;
        CramAttemptsText.Visibility = visibility;
    }

    private void SetReviewControlsVisibility(bool isVisible)
    {
        Visibility visibility = ResponsiveLayout.VisibleWhen(isVisible);
        ShortcutStrip.Visibility = visibility;
        RatingsPanel.Visibility = visibility;
        StageGapBeforeShortcutsRow.Height = isVisible ? new GridLength(14) : new GridLength(0);
        StageGapBeforeRatingsRow.Height = isVisible ? new GridLength(16) : new GridLength(0);
    }

    private void SetCramCompletionOverlayVisibility(bool isVisible)
    {
        CramCompletionOverlay.Visibility = ResponsiveLayout.VisibleWhen(isVisible);
    }

    private static string GetLimitSummaryText(FlashcardReviewQueueSummaryReadModel summary)
    {
        if (summary.ReviewLimitExcluded > 0 && summary.NewLimitExcluded > 0)
        {
            return $"Review limited {summary.ReviewLimitExcluded} | New limited {summary.NewLimitExcluded}";
        }

        if (summary.ReviewLimitExcluded > 0)
        {
            return $"Review limited {summary.ReviewLimitExcluded}";
        }

        return $"New limited {summary.NewLimitExcluded}";
    }

    private void RaiseReviewCommandStateChanged()
    {
        revealAnswerCommand.RaiseCanExecuteChanged();
        rateAgainCommand.RaiseCanExecuteChanged();
        rateHardCommand.RaiseCanExecuteChanged();
        rateGoodCommand.RaiseCanExecuteChanged();
        rateEasyCommand.RaiseCanExecuteChanged();
    }

    internal void ShowCard(
        FlashcardReviewCardReadModel card,
        FlashcardReviewQueueSummaryReadModel summary)
    {
        queueSummary = summary;
        currentCard = card;
        isWaitingForDueCard = false;
        isAnswerVisible = false;
        CompletionSummaryText.Visibility = Visibility.Collapsed;
        CramCompletionSummaryText.Text = string.Empty;
        SetCramCompletionOverlayVisibility(false);
        SetReviewControlsVisibility(true);
        SetCramProgressVisibility(false);
        ExitReviewLabel.Text = "Exit Review";
        UpdateQueueSummary();
        UpdateCardState();
        LoadCurrentCard();
        Focus();
    }

    internal void ShowWaitingState(
        FlashcardReviewQueueSummaryReadModel summary,
        DateTimeOffset? nextDueUtc)
    {
        queueSummary = summary;
        currentCard = null;
        currentMultimodalContent = null;
        isWaitingForDueCard = true;
        isAnswerVisible = false;
        ReviewCardMultimodalHost.Visibility = Visibility.Collapsed;
        ReviewCardImage.Source = null;
        ReviewCardImageContainer.Visibility = Visibility.Collapsed;
        ReviewCardMaskCanvas.Children.Clear();
        ReviewCardText.Text = "You're caught up for the moment.";
        WaitingStateTitleText.Text = "No cards are due right now";
        WaitingStateDetailText.Text = nextDueUtc.HasValue
            ? $"Next card due at {FormatNextDue(nextDueUtc.Value)}. Practice will resume automatically."
            : "No more cards are available for this session.";
        CompletionSummaryText.Visibility = Visibility.Collapsed;
        CramCompletionSummaryText.Text = string.Empty;
        SetCramCompletionOverlayVisibility(false);
        SetReviewControlsVisibility(true);
        SetCramProgressVisibility(false);
        ExitReviewLabel.Text = "Exit Review";
        UpdateQueueSummary();
        UpdateCardState();
        Focus();
    }

    internal void ShowCramEmptyState(FlashcardReviewQueueSummaryReadModel summary)
    {
        queueSummary = summary;
        currentCard = null;
        currentMultimodalContent = null;
        isWaitingForDueCard = true;
        isAnswerVisible = false;
        ReviewCardMultimodalHost.Visibility = Visibility.Collapsed;
        ReviewCardImage.Source = null;
        ReviewCardImageContainer.Visibility = Visibility.Collapsed;
        ReviewCardMaskCanvas.Children.Clear();
        ReviewCardText.Text = "No cards match this cram setup.";
        WaitingStateTitleText.Text = "No cram cards available";
        WaitingStateDetailText.Text = summary.SkippedInvalidCards > 0
            ? $"{summary.SkippedInvalidCards} selected card(s) could not be loaded."
            : "Adjust the deck selection or create cards before starting cramming.";
        CompletionSummaryText.Visibility = Visibility.Collapsed;
        CramCompletionSummaryText.Text = string.Empty;
        SetCramCompletionOverlayVisibility(false);
        SetReviewControlsVisibility(true);
        SetCramProgressVisibility(false);
        ExitReviewLabel.Text = "Close";
        UpdateQueueSummary();
        UpdateCardState();
        Focus();
    }

    internal void ShowCramCompletion(CramCompletionSummary summary)
    {
        queueSummary = new FlashcardReviewQueueSummaryReadModel(0, 0, 0, 0, 0, 0, 0, 0, summary.CardsMastered)
        {
            SelectedCards = summary.CardsSelected,
            SkippedInvalidCards = summary.CardsSkippedInvalid,
        };
        currentCard = null;
        currentMultimodalContent = null;
        isWaitingForDueCard = false;
        isAnswerVisible = false;
        ReviewCardMultimodalHost.Visibility = Visibility.Collapsed;
        ReviewCardImage.Source = null;
        ReviewCardImageContainer.Visibility = Visibility.Collapsed;
        ReviewCardMaskCanvas.Children.Clear();
        ReviewCardText.Text = string.Empty;
        CompletionSummaryText.Visibility = Visibility.Collapsed;
        CramCompletionSummaryText.Text = FormatCompletionSummary(summary);
        SetCramCompletionOverlayVisibility(true);
        SetReviewControlsVisibility(false);
        SetCramProgressVisibility(false);
        ExitReviewLabel.Text = "Close";
        UpdateQueueSummary();
        UpdateCardState();
        Focus();
    }

    internal void SetRatingInProgress(bool isInProgress)
    {
        isRatingInProgress = isInProgress;
        RaiseReviewCommandStateChanged();
    }

    private static string FormatNextDue(DateTimeOffset dueUtc)
    {
        DateTimeOffset localDue = dueUtc.ToLocalTime();
        DateTimeOffset localNow = DateTimeOffset.Now;
        return localDue.Date == localNow.Date
            ? localDue.ToString("h:mm tt")
            : localDue.ToString("MMM d h:mm tt");
    }

    private static string FormatCompletionSummary(CramCompletionSummary summary)
    {
        return string.Join(
            Environment.NewLine,
            $"Cards selected: {summary.CardsSelected:N0}",
            $"Cards mastered: {summary.CardsMastered:N0}",
            $"Cards skipped/invalid: {summary.CardsSkippedInvalid:N0}",
            $"Total attempts: {summary.TotalAttempts:N0}",
            $"Again: {summary.AgainRatings:N0}  Hard: {summary.HardRatings:N0}  Good: {summary.GoodRatings:N0}  Easy: {summary.EasyRatings:N0}",
            $"Session duration: {FormatDuration(summary.SessionDuration)}",
            $"Required pass count: {summary.RequiredPassCount:N0}",
            $"Counting mode: {summary.PassCountingMode}");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}

internal sealed record CramReviewProgress(
    int MasteredCards,
    int ValidCards,
    int RemainingCards,
    string CurrentProgressLabel,
    int CurrentPasses,
    int RequiredPassCount,
    int CurrentCardAttempts);
