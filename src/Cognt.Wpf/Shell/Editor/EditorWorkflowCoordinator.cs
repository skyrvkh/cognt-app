using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

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
    private int editorOpenRequestId;
    private int mentionTargetsRefreshRequestId;
    private string? retryNotePath;
    private bool retrySelectInTree;

    internal EditorWorkflowCoordinator(
        MainViewModel viewModel,
        EditorView editorView,
        IShellDialogService dialogs,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        Func<Task> closeReviewIfActiveAsync,
        Action hideOverlay,
        Action<string> selectNote)
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

        this.viewModel.GetCurrentEditorContent = editorView.GetContent;
        editorView.ContentChanged += EditorView_ContentChanged;
        editorView.CreateExtractRequested += EditorView_CreateExtractRequested;
        editorView.SaveClozeRequested += EditorView_SaveClozeRequested;
        editorView.OpenImageOcclusionRequested += EditorView_OpenImageOcclusionRequested;
        editorView.MentionSearchRequested += EditorView_MentionSearchRequested;
        editorView.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        editorView.OpenMentionRequested += EditorView_OpenMentionRequested;
        editorView.CreateLinkedNoteRequested += EditorView_CreateLinkedNoteRequested;
        editorView.AttachFileRequested += EditorView_AttachFileRequested;
        editorView.ReadAttachmentRequested += EditorView_ReadAttachmentRequested;
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
                editorView.ShowFailedState($"The editor could not open this note: {ex.Message}", canRetry: true);
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
        RefreshDocumentLinks(note.Content);
        QueueMentionTargetsRefresh();

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
                dialogs.ShowWarning(
                    "Editor Not Ready",
                    $"The editor could not be cleared: {ex.Message}");
            }
        }
    }

}
