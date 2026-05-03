using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.Shell.Editor;

internal sealed partial class EditorWorkflowCoordinator
{
    private void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        string? sourcePath = e.SourcePath;

        PostEditorActionResult(
            () => viewModel.CreateExtractNote(sourcePath, e.SelectedContent, e.SelectedPlainText),
            createdPath => editorView.PostExtractResult(ok: true, createdPath),
            ex => editorView.PostExtractResult(ok: false, error: ex.Message));
    }

    private void EditorView_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        PostEditorActionResult(
            () =>
            {
                viewModel.SaveClozeDraft(
                    e.SourcePath,
                    e.BlockId,
                    e.SelectedPlainText,
                    e.ClozeText,
                    e.Content,
                    e.CardContextId,
                    e.SlotId,
                    e.DisplayClozeIndex);
                RefreshDocumentLinks(e.Content);
                return true;
            },
            _ => editorView.PostSaveResult(ok: true),
            ex => editorView.PostSaveResult(ok: false, error: ex.Message));
    }

    private async void EditorView_OpenImageOcclusionRequested(object? sender, OpenImageOcclusionRequestedEventArgs e)
    {
        await OpenImageOcclusionForImageAsync(
            e.ImageBlockId,
            e.ImageRelPath,
            e.Content);
    }

    private void EditorView_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        e.Complete(viewModel.SearchMentionTargets(e.Query, e.Limit));
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        PostEditorActionResult(
            () => viewModel.ReadMentionPreview(e.RelativePath, e.MaxCharacters),
            preview => editorView.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: true,
                documentContent: preview),
            ex => editorView.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: ex.Message));
    }

    private async void EditorView_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        try
        {
            viewModel.SaveMentionSource(e.SourcePath, e.Content);
            string targetPath = viewModel.ResolveMentionPath(e.RelativePath);
            bool opened = await OpenNoteAsync(targetPath, selectInTree: true);
            editorView.PostMentionOpenResult(
                ok: opened,
                error: opened ? null : "The mentioned note could not be opened.");
        }
        catch (Exception ex)
        {
            editorView.PostMentionOpenResult(ok: false, error: ex.Message);
        }
    }

    private void EditorView_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        PostEditorActionResult(
            () => viewModel.CreateLinkedNote(e.SourcePath),
            e.Complete,
            ex => e.Complete(CreateLinkedNoteResult.Fail(ex.Message)));
    }

    private async Task EditorView_AttachFileRequested(object? sender, AttachFileRequestedEventArgs e)
    {
        try
        {
            byte[] content = Convert.FromBase64String(e.ContentBase64);
            using MemoryStream stream = new(content, writable: false);
            KnowledgeAttachmentReadModel attachment = await viewModel.AttachAttachmentStreamToCurrentNoteAsync(
                e.FileName,
                stream);

            e.Complete(AttachFileResult.Success(
                attachment.OriginalFileName,
                attachment.RelativePath,
                attachment.Kind.ToString()));
        }
        catch (Exception ex)
        {
            e.Complete(AttachFileResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        try
        {
            ReadAttachmentResult result = await viewModel.ReadImageAttachmentForCurrentNoteAsync(
                e.SourcePath,
                e.ImageSource);
            e.Complete(result);
        }
        catch (Exception ex)
        {
            e.Complete(ReadAttachmentResult.Fail(ex.Message));
        }
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        PostEditorActionResult(
            () =>
            {
                viewModel.SaveNoteIfChanged(content);
                RefreshDocumentLinks(content);
                return true;
            },
            _ => { },
            ex => editorView.PostSaveResult(ok: false, error: ex.Message));
    }

    private void RefreshDocumentLinks(string currentContent)
    {
        try
        {
            editorView.PostDocumentLinks(viewModel.ReadCurrentDocumentLinks(currentContent));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Document links refresh failed: {ex}");
            editorView.PostDocumentLinks(new KnowledgeDocumentLinksReadModel([], []));
        }
    }

    private static void PostEditorActionResult<TResult>(
        Func<TResult> action,
        Action<TResult> onSuccess,
        Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            onSuccess(action());
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }
}
