using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Review;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp.Shell.Overlay;

internal sealed partial class OverlayNavigationCoordinator
{
    private readonly IShellDialogService dialogs;
    private readonly MainViewModel viewModel;
    private readonly AllNotesViewModel allNotesViewModel;
    private readonly GraphViewModel graphViewModel;
    private readonly DashboardView dashboardView;
    private readonly AllNotesView allNotesView;
    private readonly GraphView graphView;
    private readonly DecksView decksView;
    private readonly SettingsView settingsView;
    private readonly OverlayHostController overlayHost;
    private readonly EditorView editorView;
    private readonly ReviewOverlayCoordinator reviewOverlay;
    private int editorOpenRequestId;
    private PdfReaderView? pdfReaderView;

    internal OverlayNavigationCoordinator(
        IShellDialogService dialogs,
        MainViewModel viewModel,
        AllNotesViewModel allNotesViewModel,
        GraphViewModel graphViewModel,
        DashboardView dashboardView,
        AllNotesView allNotesView,
        GraphView graphView,
        DecksView decksView,
        SettingsView settingsView,
        OverlayHostController overlayHost,
        EditorView editorView,
        ReviewOverlayCoordinator reviewOverlay)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(allNotesViewModel);
        ArgumentNullException.ThrowIfNull(graphViewModel);
        ArgumentNullException.ThrowIfNull(dashboardView);
        ArgumentNullException.ThrowIfNull(allNotesView);
        ArgumentNullException.ThrowIfNull(graphView);
        ArgumentNullException.ThrowIfNull(decksView);
        ArgumentNullException.ThrowIfNull(settingsView);
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(reviewOverlay);

        this.dialogs = dialogs;
        this.viewModel = viewModel;
        this.allNotesViewModel = allNotesViewModel;
        this.graphViewModel = graphViewModel;
        this.dashboardView = dashboardView;
        this.allNotesView = allNotesView;
        this.graphView = graphView;
        this.decksView = decksView;
        this.settingsView = settingsView;
        this.overlayHost = overlayHost;
        this.editorView = editorView;
        this.reviewOverlay = reviewOverlay;
    }

    internal async Task HandleOverlayRequestedAsync(OverlayRequestedEventArgs e)
    {
        try
        {
            editorOpenRequestId++;

            switch (e.ViewKind)
            {
                case OverlayViewKind.AllNotes:
                    await CloseOverlayWorkflowsAsync();
                    await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
                    ShowOverlay(allNotesView);
                    return;

                case OverlayViewKind.Graph:
                    await CloseOverlayWorkflowsAsync();
                    await graphViewModel.LoadAsync();
                    ShowOverlay(graphView);
                    return;

                case OverlayViewKind.Decks:
                    await CloseReviewIfActiveAsync();
                    ShowDecksOverlay();
                    return;

                case OverlayViewKind.Review:
                    await TryShowReviewOverlayAsync(editorOpenRequestId);
                    return;

                case OverlayViewKind.Settings:
                    await CloseOverlayWorkflowsAsync();
                    ShowOverlay(settingsView);
                    return;

                default:
                    await CloseOverlayWorkflowsAsync();
                    ShowOverlay(dashboardView);
                    return;
            }
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Navigation Failed", $"The view could not be changed: {ex.Message}");
            editorView.AttachBlockSuiteHostToEditor();
        }
    }

    internal async Task HandleOpenPdfRequestedAsync(OpenPdfRequestedEventArgs e)
    {
        try
        {
            editorOpenRequestId++;
            await CloseOverlayWorkflowsAsync();
            pdfReaderView ??= new PdfReaderView();
            ShowOverlay(pdfReaderView);
            string documentPath = viewModel.ResolveDocumentPath(e.Document.RelativePath);
            await pdfReaderView.LoadPdfAsync(e.Document, documentPath);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Open PDF Failed", $"The PDF could not be opened:{Environment.NewLine}{ex.Message}");
        }
    }

    internal Task HandleAllNotesOpenRequestedAsync(OpenAllNotesRequestedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        try
        {
            HideOverlay();
            viewModel.OpenNoteByRelativePath(e.RelativePath);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    internal void HideOverlay()
    {
        overlayHost.Hide();
    }

    internal Task CloseReviewIfActiveAsync()
    {
        return reviewOverlay.CloseReviewIfActiveAsync();
    }
}

