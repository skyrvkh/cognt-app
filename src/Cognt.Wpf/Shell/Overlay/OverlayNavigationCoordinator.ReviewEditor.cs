using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Overlay;

internal sealed partial class OverlayNavigationCoordinator
{
    internal async Task HandlePracticeRequestedAsync()
    {
        int requestId = ++editorOpenRequestId;
        viewModel.SaveCurrentNoteFromEditor();
        await TryShowReviewOverlayAsync(requestId);
    }

    internal async Task HandleCramRequestedAsync(CramScope scope, CrammingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        int requestId = ++editorOpenRequestId;
        viewModel.SaveCurrentNoteFromEditor();
        await TryShowCramOverlayAsync(requestId, scope, settings);
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
            logger.LogError(ex, "Review exit failed. RequestId={RequestId}", editorOpenRequestId);
            dialogs.ShowWarning("Review", $"Review mode could not close cleanly: {ex.Message}");
            editorView.AttachBlockSuiteHostToEditor();
            ShowDecksOverlay();
        }
    }
}
