using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Models.Navigation;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Overlay;
using CogntApp.Views;

namespace CogntApp.Shell.Review;

internal sealed partial class ReviewOverlayCoordinator
{
    private readonly OverlayHostController overlayHost;
    private readonly IShellDialogService dialogs;
    private readonly IFlashcardReviewQueueQuery reviewQueueQuery;
    private readonly IRecordFlashcardReviewCommand recordFlashcardReviewCommand;
    private readonly Func<string> collectionIdProvider;
    private ReviewView? reviewView;
    private string? activeCollectionId;

    internal ReviewOverlayCoordinator(
        OverlayHostController overlayHost,
        IShellDialogService dialogs,
        IFlashcardReviewQueueQuery reviewQueueQuery,
        IRecordFlashcardReviewCommand recordFlashcardReviewCommand,
        Func<string> collectionIdProvider)
    {
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(reviewQueueQuery);
        ArgumentNullException.ThrowIfNull(recordFlashcardReviewCommand);
        ArgumentNullException.ThrowIfNull(collectionIdProvider);

        this.overlayHost = overlayHost;
        this.dialogs = dialogs;
        this.reviewQueueQuery = reviewQueueQuery;
        this.recordFlashcardReviewCommand = recordFlashcardReviewCommand;
        this.collectionIdProvider = collectionIdProvider;
    }

    internal event EventHandler? ExitReviewRequested;

    internal async Task ShowReviewOverlayAsync()
    {
        string collectionId = GetRequiredCollectionId();
        FlashcardReviewQueueReadModel reviewQueue = LoadReviewQueue(collectionId);
        EnsureReviewQueueAvailable(reviewQueue);

        activeCollectionId = collectionId;
        ReviewView review = EnsureReviewView();
        overlayHost.Show(review);
        await ShowReviewQueueAsync(reviewQueue, exitWhenUnavailable: false);
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
            await recordFlashcardReviewCommand.ExecuteAsync(
                new RecordFlashcardReviewRequest(collectionId, e.CardId, e.Rating));
            await ReloadReviewQueueAsync(collectionId, exitWhenUnavailable: true);
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
