using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Models.Navigation;
using CogntApp.ViewModels;

namespace CogntApp.Views;

public partial class ReviewView : UserControl
{
    private readonly RelayCommand revealAnswerCommand;
    private readonly RelayCommand rateAgainCommand;
    private readonly RelayCommand rateHardCommand;
    private readonly RelayCommand rateGoodCommand;
    private readonly RelayCommand rateEasyCommand;
    private FlashcardReviewCardReadModel? currentCard;
    private bool isAnswerVisible;
    private bool isRatingInProgress;
    private bool isDisposed;
    private bool isWaitingForDueCard;
    private FlashcardReviewQueueSummaryReadModel? queueSummary;

    internal event EventHandler? ExitReviewRequested;
    internal event EventHandler<ReviewRatingRequestedEventArgs>? RatingRequested;
    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    public ICommand RevealAnswerCommand { get; }

    public ICommand RateAgainCommand { get; }

    public ICommand RateHardCommand { get; }

    public ICommand RateGoodCommand { get; }

    public ICommand RateEasyCommand { get; }

    internal ReviewView()
    {
        revealAnswerCommand = new RelayCommand(_ => ExecuteRevealAnswerCommand(), _ => CanRevealAnswer);
        rateAgainCommand = new RelayCommand(_ => ExecuteRateCommand(FlashcardReviewRatingReadModel.Again), _ => CanRateCurrentCard);
        rateHardCommand = new RelayCommand(_ => ExecuteRateCommand(FlashcardReviewRatingReadModel.Hard), _ => CanRateCurrentCard);
        rateGoodCommand = new RelayCommand(_ => ExecuteRateCommand(FlashcardReviewRatingReadModel.Good), _ => CanRateCurrentCard);
        rateEasyCommand = new RelayCommand(_ => ExecuteRateCommand(FlashcardReviewRatingReadModel.Easy), _ => CanRateCurrentCard);

        RevealAnswerCommand = revealAnswerCommand;
        RateAgainCommand = rateAgainCommand;
        RateHardCommand = rateHardCommand;
        RateGoodCommand = rateGoodCommand;
        RateEasyCommand = rateEasyCommand;

        InitializeComponent();
    }

    private void ExecuteRevealAnswerCommand()
    {
        try
        {
            RevealAnswer();
        }
        catch (Exception ex)
        {
            WarningMessageRequested?.Invoke(
                this,
                new UserMessageRequestedEventArgs("Review Renderer Not Ready", $"The card answer could not be shown: {ex.Message}"));
        }
    }

    private void ExecuteRateCommand(FlashcardReviewRatingReadModel rating)
    {
        try
        {
            RateCurrentCard(rating);
        }
        catch (Exception ex)
        {
            WarningMessageRequested?.Invoke(
                this,
                new UserMessageRequestedEventArgs("Review", $"The review card could not be advanced: {ex.Message}"));
        }
    }

    internal void DisposeReview()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        currentCard = null;
        isWaitingForDueCard = false;
        isAnswerVisible = false;
        isRatingInProgress = false;
        queueSummary = null;
        currentMultimodalContent = null;
        ReviewCardImage.Source = null;
        ReviewCardMaskCanvas.Children.Clear();
        RaiseReviewCommandStateChanged();
    }

    private void RevealAnswer()
    {
        if (!CanRevealAnswer)
        {
            return;
        }

        isAnswerVisible = true;
        UpdateCardState();
        LoadCurrentCard();
    }

    private void RateCurrentCard(FlashcardReviewRatingReadModel rating)
    {
        if (!CanRateCurrentCard)
        {
            return;
        }

        FlashcardReviewCardReadModel card = currentCard
            ?? throw new InvalidOperationException("There is no active review card.");
        RatingRequested?.Invoke(this, new ReviewRatingRequestedEventArgs(card.Id, rating));
    }

}

internal sealed class ReviewRatingRequestedEventArgs : EventArgs
{
    public ReviewRatingRequestedEventArgs(long cardId, FlashcardReviewRatingReadModel rating)
    {
        CardId = cardId;
        Rating = rating;
    }

    public long CardId { get; }

    public FlashcardReviewRatingReadModel Rating { get; }
}
