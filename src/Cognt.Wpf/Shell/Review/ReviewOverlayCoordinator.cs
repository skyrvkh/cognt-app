using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Overlay;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp.Shell.Review;

internal sealed partial class ReviewOverlayCoordinator
{
    private readonly OverlayHostController overlayHost;
    private readonly IShellDialogService dialogs;
    private readonly IFlashcardReviewQueueQuery reviewQueueQuery;
    private readonly IRecordFlashcardReviewCommand recordFlashcardReviewCommand;
    private readonly ICramSessionEngine cramSessionEngine;
    private readonly Func<string> collectionIdProvider;
    private ReviewView? reviewView;
    private string? activeCollectionId;
    private FlashcardReviewQueueMode activeQueueMode = FlashcardReviewQueueMode.Scheduled;
    private CramScope activeCramScope = CramScope.All;
    private Dictionary<long, FlashcardReviewCardReadModel> activeCramCardsById = [];
    private FlashcardReviewQueueSummaryReadModel? activeCramSummary;
    private CramSessionState? activeCramState;
    private HashSet<long> activeCramSchedulerRecordedCardIds = [];

    internal ReviewOverlayCoordinator(
        OverlayHostController overlayHost,
        IShellDialogService dialogs,
        IFlashcardReviewQueueQuery reviewQueueQuery,
        IRecordFlashcardReviewCommand recordFlashcardReviewCommand,
        ICramSessionEngine cramSessionEngine,
        Func<string> collectionIdProvider)
    {
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(reviewQueueQuery);
        ArgumentNullException.ThrowIfNull(recordFlashcardReviewCommand);
        ArgumentNullException.ThrowIfNull(cramSessionEngine);
        ArgumentNullException.ThrowIfNull(collectionIdProvider);

        this.overlayHost = overlayHost;
        this.dialogs = dialogs;
        this.reviewQueueQuery = reviewQueueQuery;
        this.recordFlashcardReviewCommand = recordFlashcardReviewCommand;
        this.cramSessionEngine = cramSessionEngine;
        this.collectionIdProvider = collectionIdProvider;
    }

    internal event EventHandler? ExitReviewRequested;

    internal async Task ShowReviewOverlayAsync()
    {
        string collectionId = GetRequiredCollectionId();
        activeQueueMode = FlashcardReviewQueueMode.Scheduled;
        activeCramScope = CramScope.All;
        ClearCramSession();
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);
        EnsureReviewQueueAvailable(reviewQueue);

        activeCollectionId = collectionId;
        ReviewView review = EnsureReviewView();
        overlayHost.Show(review);
        await ShowReviewQueueAsync(reviewQueue, exitWhenUnavailable: false);
    }

    internal async Task ShowCramOverlayAsync(CramScope scope, CrammingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        string collectionId = GetRequiredCollectionId();
        activeQueueMode = FlashcardReviewQueueMode.Cram;
        activeCramScope = scope.Normalize();
        StopQueueRefreshTimer();
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);

        activeCollectionId = collectionId;
        ReviewView review = EnsureReviewView();
        overlayHost.Show(review);
        if (reviewQueue.Cards.Count == 0)
        {
            StartCramSession(reviewQueue, settings);
            review.ShowCramEmptyState(reviewQueue.Summary);
            return;
        }

        StartCramSession(reviewQueue, settings);
        await ShowMaterializedCramQueueAsync(exitWhenUnavailable: false);
    }

    internal Task CloseReviewIfActiveAsync()
    {
        if (reviewView is null)
        {
            return Task.CompletedTask;
        }

        ReviewView view = reviewView;
        reviewView = null;
        activeCollectionId = null;
        activeQueueMode = FlashcardReviewQueueMode.Scheduled;
        activeCramScope = CramScope.All;
        ClearCramSession();
        StopQueueRefreshTimer();

        overlayHost.ClearIfContent(view);

        view.ExitReviewRequested -= ReviewView_ExitReviewRequested;
        view.RatingRequested -= ReviewView_RatingRequested;
        view.WarningMessageRequested -= ReviewView_WarningMessageRequested;
        view.DisposeReview();
        return Task.CompletedTask;
    }

    private ReviewView EnsureReviewView()
    {
        if (reviewView is null)
        {
            reviewView = new ReviewView();
            reviewView.ExitReviewRequested += ReviewView_ExitReviewRequested;
            reviewView.RatingRequested += ReviewView_RatingRequested;
            reviewView.WarningMessageRequested += ReviewView_WarningMessageRequested;
        }

        return reviewView;
    }

    private void ReviewView_ExitReviewRequested(object? sender, EventArgs e)
    {
        ExitReviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReviewView_WarningMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        dialogs.ShowWarning(e.Title, e.Message);
    }

    private async void ReviewView_RatingRequested(object? sender, ReviewRatingRequestedEventArgs e)
    {
        ReviewView? review = reviewView;
        if (review is null)
        {
            return;
        }

        string collectionId = activeCollectionId
            ?? throw new InvalidOperationException("No active review session exists.");

        review.SetRatingInProgress(true);
        try
        {
            if (activeQueueMode == FlashcardReviewQueueMode.Cram)
            {
                await RecordCramRatingAsync(collectionId, e);
            }
            else
            {
                await recordFlashcardReviewCommand.ExecuteAsync(
                    new RecordFlashcardReviewRequest(collectionId, e.CardId, e.Rating));
                await ReloadReviewQueueAsync(collectionId, exitWhenUnavailable: true);
            }
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Review", $"The review card could not be advanced: {ex.Message}");
        }
        finally
        {
            review.SetRatingInProgress(false);
        }
    }

    private void StartCramSession(
        FlashcardReviewQueueReadModel reviewQueue,
        CrammingSettings settings)
    {
        int cardsSelected = Math.Max(
            reviewQueue.Summary.SelectedCards,
            reviewQueue.Cards.Count + reviewQueue.Summary.SkippedInvalidCards);
        activeCramCardsById = reviewQueue.Cards.ToDictionary(card => card.Id);
        activeCramSummary = reviewQueue.Summary;
        activeCramState = cramSessionEngine.Start(
            reviewQueue.Cards.Select(card => card.Id).ToArray(),
            settings,
            cardsSelected,
            reviewQueue.Summary.SkippedInvalidCards);
        activeCramSchedulerRecordedCardIds = [];
    }

    private void ClearCramSession()
    {
        activeCramCardsById = [];
        activeCramSummary = null;
        activeCramState = null;
        activeCramSchedulerRecordedCardIds = [];
    }

    private async Task RecordCramRatingAsync(
        string collectionId,
        ReviewRatingRequestedEventArgs e)
    {
        CramSessionState state = activeCramState
            ?? throw new InvalidOperationException("No active cram session exists.");
        CramRatingResult result = cramSessionEngine.RateCurrent(state, e.Rating);
        activeCramState = result.State;

        if (state.Settings.AffectNormalSchedule
            && activeCramSchedulerRecordedCardIds.Add(e.CardId))
        {
            await recordFlashcardReviewCommand.ExecuteAsync(
                new RecordFlashcardReviewRequest(collectionId, e.CardId, e.Rating));
        }

        if (result.CompletionSummary is not null)
        {
            ReviewView review = reviewView
                ?? throw new InvalidOperationException("The review surface is not ready.");
            review.ShowCramCompletion(result.CompletionSummary);
            return;
        }

        await ShowMaterializedCramQueueAsync(exitWhenUnavailable: true);
    }

    private string GetRequiredCollectionId()
    {
        string collectionId = collectionIdProvider();
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            throw new InvalidOperationException("Open a collection before reviewing flashcards.");
        }

        return collectionId;
    }
}
