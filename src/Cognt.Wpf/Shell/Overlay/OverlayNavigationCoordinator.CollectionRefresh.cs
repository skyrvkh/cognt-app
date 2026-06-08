namespace CogntApp.Shell.Overlay;

internal sealed partial class OverlayNavigationCoordinator
{
    internal async Task RefreshActiveCollectionViewAsync()
    {
        editorOpenRequestId++;
        bool hadReviewOverlay = reviewOverlay.IsReviewActive;
        await CloseReviewIfActiveAsync();
        if (hadReviewOverlay)
        {
            ShowDecksOverlay();
            return;
        }

        if (overlayHost.IsShowing(allNotesView))
        {
            await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
            return;
        }

        if (overlayHost.IsShowing(graphView))
        {
            await graphViewModel.LoadAsync();
            return;
        }

        if (overlayHost.IsShowing(decksView))
        {
            ShowDecksOverlay();
            return;
        }
    }
}
