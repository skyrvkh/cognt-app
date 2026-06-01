using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed partial class EditorWorkflowCoordinator
{
    private readonly MainViewModel viewModel;
    private readonly EditorView editorView;
    private readonly IShellDialogService dialogs;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
    private readonly Func<Task> closeReviewIfActiveAsync;
    private readonly Action hideOverlay;
    private readonly Action<string> selectNote;
    private readonly EditorDerivedDataRefreshController refreshes;
    private readonly EditorHostRequestController hostRequests;
    private readonly ILogger<EditorWorkflowCoordinator>? logger;
    private int editorOpenRequestId;
    private string? retryNotePath;
    private bool retrySelectInTree;

    internal EditorWorkflowCoordinator(
        MainViewModel viewModel,
        EditorView editorView,
        IShellDialogService dialogs,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        Func<Task> closeReviewIfActiveAsync,
        Action hideOverlay,
        Action<string> selectNote,
        ILogger<EditorWorkflowCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(imageMaskEditorDialogs);
        ArgumentNullException.ThrowIfNull(closeReviewIfActiveAsync);
        ArgumentNullException.ThrowIfNull(hideOverlay);
        ArgumentNullException.ThrowIfNull(selectNote);

        this.viewModel = viewModel;
        this.editorView = editorView;
        this.dialogs = dialogs;
        this.imageMaskEditorDialogs = imageMaskEditorDialogs;
        this.closeReviewIfActiveAsync = closeReviewIfActiveAsync;
        this.hideOverlay = hideOverlay;
        this.selectNote = selectNote;
        this.logger = logger;
        refreshes = new EditorDerivedDataRefreshController(
            viewModel,
            editorView,
            dialogs,
            logger);
        hostRequests = new EditorHostRequestController(
            viewModel,
            editorView,
            refreshes,
            OpenNoteAsync,
            OpenImageOcclusionForImageAsync,
            logger);

        this.viewModel.GetCurrentEditorContent = editorView.GetContent;
        editorView.RetryRequested += EditorView_RetryRequested;
    }

    internal async Task<bool> OpenNoteAsync(string notePath, bool selectInTree)
    {
        if (string.Equals(viewModel.CurrentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        retryNotePath = notePath;
        retrySelectInTree = selectInTree;
        editorView.ShowLoadingState("Loading note", "Opening the selected note...");

        PreparedNote? note;

        try
        {
            note = viewModel.PrepareNoteForOpen(notePath);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Editor workflow note preparation failed. NotePath={NotePath}", notePath);
            editorView.ShowFailedState($"The note could not be opened: {ex.Message}", canRetry: true);
            return false;
        }

        if (note is null)
        {
            retryNotePath = null;
            return true;
        }

        int requestId = ++editorOpenRequestId;
        await closeReviewIfActiveAsync();

        try
        {
            editorView.AttachBlockSuiteHostToEditor();
            await editorView.LoadNoteAsync(
                note.Title,
                note.Content,
                note.NotePath,
                viewModel.OpenCollectionPath);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                logger?.LogError(ex, "Editor workflow note load failed. RequestId={RequestId} NotePath={NotePath}", requestId, note.NotePath);
                editorView.ShowFailedState($"The editor could not open this note: {ex.Message}", canRetry: true);
            }
            else
            {
            }

            return false;
        }

        if (requestId != editorOpenRequestId)
        {
            return false;
        }

        viewModel.CompleteOpenNote(note);
        retryNotePath = null;
        hideOverlay();
        refreshes.RefreshDocumentLinks(note.Content);
        refreshes.QueueMentionTargetsRefresh();

        if (selectInTree)
        {
            selectNote(note.NotePath);
        }

        return true;
    }

    internal async Task ShowEmptyEditorViewAsync()
    {
        int requestId = ++editorOpenRequestId;
        retryNotePath = null;
        await closeReviewIfActiveAsync();
        hideOverlay();

        try
        {
            editorView.AttachBlockSuiteHostToEditor();
            await editorView.ClearNoteAsync();
            editorView.PostDocumentLinks(new KnowledgeDocumentLinksReadModel([], []));
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                logger?.LogError(ex, "Editor workflow clear editor failed. RequestId={RequestId}", requestId);
                dialogs.ShowWarning(
                    "Editor Not Ready",
                    $"The editor could not be cleared: {ex.Message}");
            }
            else
            {
            }
        }
    }

}
