using System.Diagnostics.CodeAnalysis;
using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Overlay;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Review;

internal sealed partial class ReviewOverlayCoordinator
{
    private readonly OverlayHostController overlayHost;
    private readonly IShellDialogService dialogs;
    private readonly IFlashcardReviewQueueQuery reviewQueueQuery;
    private readonly IRecordFlashcardReviewCommand recordFlashcardReviewCommand;
    private readonly ICramSessionEngine cramSessionEngine;
    private readonly Func<string> collectionIdProvider;
    private readonly ILogger<ReviewOverlayCoordinator> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly ReviewOverlayQueueState queueState = new();
    private ReviewView? reviewView;
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
        Func<string> collectionIdProvider,
        ILogger<ReviewOverlayCoordinator> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(reviewQueueQuery);
        ArgumentNullException.ThrowIfNull(recordFlashcardReviewCommand);
        ArgumentNullException.ThrowIfNull(cramSessionEngine);
        ArgumentNullException.ThrowIfNull(collectionIdProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.overlayHost = overlayHost;
        this.dialogs = dialogs;
        this.reviewQueueQuery = reviewQueueQuery;
        this.recordFlashcardReviewCommand = recordFlashcardReviewCommand;
        this.cramSessionEngine = cramSessionEngine;
        this.collectionIdProvider = collectionIdProvider;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
    }

    internal event EventHandler? ExitReviewRequested;

    internal bool IsReviewActive => reviewView is not null;

    internal async Task ShowReviewOverlayAsync()
    {
        string collectionId = GetRequiredCollectionId();
        queueState.BeginScheduled(collectionId);
        ClearCramSession();
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);
        queueState.CacheScheduledQueue(reviewQueue);
        EnsureReviewQueueAvailable(reviewQueue);

        ReviewView review = EnsureReviewView();
        overlayHost.Show(review);
        await ShowReviewQueueAsync(reviewQueue, exitWhenUnavailable: false);
    }

    internal async Task ShowCramOverlayAsync(CramScope scope, CrammingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        string collectionId = GetRequiredCollectionId();
        queueState.BeginCram(collectionId, scope);
        StopQueueRefreshTimer();
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);

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
        queueState.Reset();
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
            reviewView = new ReviewView(loggerFactory.CreateLogger<ReviewView>());
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

        string collectionId = queueState.ActiveCollectionId
            ?? throw new InvalidOperationException("No active review session exists.");

        review.SetRatingInProgress(true);
        try
        {
            if (queueState.ActiveQueueMode == FlashcardReviewQueueMode.Cram)
            {
                await RecordCramRatingAsync(collectionId, e);
            }
            else
            {
                RecordedFlashcardReviewReadModel reviewResult = await recordFlashcardReviewCommand.ExecuteAsync(
                    new RecordFlashcardReviewRequest(collectionId, e.CardId, e.Rating));
                if (TryAdvanceScheduledQueueFromCache(reviewResult, out FlashcardReviewQueueReadModel? cachedQueue))
                {
                    await ShowReviewQueueAsync(cachedQueue, exitWhenUnavailable: true);
                }
                else
                {
                    await ReloadReviewQueueAsync(collectionId, exitWhenUnavailable: true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review rating failed. CollectionId={CollectionId} CardId={CardId} Rating={Rating}", collectionId, e.CardId, e.Rating);
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
        (activeCramCardsById, long[] cardIds) = MaterializeCramCards(reviewQueue.Cards);
        activeCramSummary = reviewQueue.Summary;
        activeCramState = cramSessionEngine.Start(
            cardIds,
            settings,
            cardsSelected,
            reviewQueue.Summary.SkippedInvalidCards);
        activeCramSchedulerRecordedCardIds = [];
    }

    private static (Dictionary<long, FlashcardReviewCardReadModel> CardsById, long[] CardIds) MaterializeCramCards(
        IReadOnlyList<FlashcardReviewCardReadModel> cards)
    {
        Dictionary<long, FlashcardReviewCardReadModel> cardsById = new(cards.Count);
        long[] cardIds = new long[cards.Count];
        for (int index = 0; index < cards.Count; index++)
        {
            FlashcardReviewCardReadModel card = cards[index];
            cardsById.Add(card.Id, card);
            cardIds[index] = card.Id;
        }

        return (cardsById, cardIds);
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
            _ = await recordFlashcardReviewCommand.ExecuteAsync(
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

    private bool TryAdvanceScheduledQueueFromCache(
        RecordedFlashcardReviewReadModel reviewResult,
        [NotNullWhen(true)]
        out FlashcardReviewQueueReadModel? reviewQueue)
    {
        return queueState.TryAdvanceScheduledQueue(reviewResult, DateTimeOffset.UtcNow, out reviewQueue);
    }
}
