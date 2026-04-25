using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Shell.Overlay;

internal sealed partial class OverlayNavigationCoordinator
{
    internal async Task HandlePracticeRequestedAsync()
    {
        int requestId = ++editorOpenRequestId;
        viewModel.SaveCurrentNoteFromEditor();
        await TryShowReviewOverlayAsync(requestId);
    }

    internal async Task HandleCreateCardEditorHostRequestedAsync(CreateCardEditorHostRequestedEventArgs e)
    {
        int requestId = ++editorOpenRequestId;

        try
        {
            await CloseReviewIfActiveAsync();
            await editorView.EditTransientNoteInHostAsync(
                e.Host,
                "Card Text",
                e.Content,
                e.ContentChanged);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                dialogs.ShowWarning("Card Editor Not Ready", $"The card editor could not start: {ex.Message}");
            }
        }
    }

    internal async Task HandleCreateCardEditorReleasedAsync()
    {
        try
        {
            await CloseCreateCardEditorIfActiveAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Card editor restore failed: {ex}");
            editorView.AttachBlockSuiteHostToEditor();
        }
    }

    internal async Task HandleReviewExitRequestedAsync()
    {
        try
        {
            editorOpenRequestId++;
            await CloseReviewIfActiveAsync();
            ShowDecksOverlay();
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Review", $"Review mode could not close cleanly: {ex.Message}");
            editorView.AttachBlockSuiteHostToEditor();
            ShowDecksOverlay();
        }
    }
}
