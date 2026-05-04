using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

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
        QueueNewText.Text = $"New {summary.NewCards}";
        QueueSuspendedText.Text = $"Suspended skipped {summary.SuspendedCardsExcluded}";
        QueueLimitedText.Text = GetLimitSummaryText(summary);
        QueueTotalText.Text = $"Session {summary.TotalCards}";
        QueueRelearningText.Visibility = ResponsiveLayout.VisibleWhen(summary.RelearningDueCards > 0);
        QueueOverdueText.Visibility = ResponsiveLayout.VisibleWhen(summary.OverdueCards > 0);
        QueueSuspendedText.Visibility = ResponsiveLayout.VisibleWhen(summary.SuspendedCardsExcluded > 0);
        QueueLimitedText.Visibility = ResponsiveLayout.VisibleWhen(
            summary.ReviewLimitExcluded + summary.NewLimitExcluded > 0);
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
}
