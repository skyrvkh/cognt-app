using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Overlay;

internal sealed partial class OverlayNavigationCoordinator
{
    private async Task CloseOverlayWorkflowsAsync(bool closeReview = true, bool closeCreateCardEditor = true)
    {
        if (closeReview)
        {
            await CloseReviewIfActiveAsync();
        }

        if (closeCreateCardEditor)
        {
            await CloseCreateCardEditorIfActiveAsync();
        }
    }

    private Task CloseCreateCardEditorIfActiveAsync()
    {
        return editorView.IsShowingTransientEditor
            ? editorView.RestoreNoteAfterTransientEditorAsync()
            : Task.CompletedTask;
    }

    private async Task TryShowReviewOverlayAsync(int requestId)
    {
        try
        {
            await CloseCreateCardEditorIfActiveAsync();
            await reviewOverlay.ShowReviewOverlayAsync();
        }
        catch (Exception ex)
        {
            if (requestId != editorOpenRequestId)
            {
                return;
            }

            dialogs.ShowWarning("Review Not Ready", $"Review mode could not start: {ex.Message}");
            await CloseReviewIfActiveAsync();
            ShowDecksOverlay();
        }
    }

    private async Task TryShowCramOverlayAsync(
        int requestId,
        CramScope scope,
        CrammingSettings settings)
    {
        try
        {
            await CloseCreateCardEditorIfActiveAsync();
            await reviewOverlay.ShowCramOverlayAsync(scope, settings);
        }
        catch (Exception ex)
        {
            if (requestId != editorOpenRequestId)
            {
                return;
            }

            dialogs.ShowWarning("Cram Not Ready", $"Cram mode could not start: {ex.Message}");
            await CloseReviewIfActiveAsync();
            ShowDecksOverlay();
        }
    }

    private void ShowOverlay(UIElement overlay)
    {
        overlayHost.Show(overlay);
    }

    private void ShowDecksOverlay()
    {
        FlashcardSourceContext sourceContext = viewModel.GetFlashcardSourceContext();
        decksView.SetCollectionContext(
            viewModel.OpenCollectionPath,
            sourceContext.SourceRelPath,
            sourceContext.SourceLabel);
        ShowOverlay(decksView);
    }
}
