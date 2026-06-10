using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class EditorAssetRequestController
{
    private readonly MainViewModel viewModel;
    private readonly ILogger? logger;

    internal EditorAssetRequestController(
        MainViewModel viewModel,
        EditorView editorView,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);

        this.viewModel = viewModel;
        this.logger = logger;

        editorView.AttachFileRequested += EditorView_AttachFileRequested;
        editorView.ReadAttachmentRequested += EditorView_ReadAttachmentRequested;
        editorView.StoreEditorBlobRequested += EditorView_StoreEditorBlobRequested;
        editorView.ReadEditorBlobRequested += EditorView_ReadEditorBlobRequested;
        editorView.ListEditorBlobsRequested += EditorView_ListEditorBlobsRequested;
    }

    private async Task EditorView_AttachFileRequested(object? sender, AttachFileRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            async () =>
            {
                byte[] content = Convert.FromBase64String(e.ContentBase64);
                using MemoryStream stream = new(content, writable: false);
                KnowledgeAttachmentReadModel attachment = await viewModel.AttachAttachmentStreamToNoteAsync(
                    e.SourcePath,
                    e.FileName,
                    stream);

                return AttachFileResult.Success(
                    attachment.OriginalFileName,
                    attachment.RelativePath,
                    attachment.Kind.ToString());
            },
            e.Complete,
            ex =>
            {
                logger?.LogError(ex, "Editor attach file failed. FileName={FileName}", e.FileName);
                e.Complete(AttachFileResult.Fail(ex.Message));
            });
    }

    private async Task EditorView_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            () => viewModel.ReadImageAttachmentForCurrentNoteAsync(
                e.SourcePath,
                e.ImageSource),
            e.Complete,
            ex =>
            {
                logger?.LogError(ex, "Editor read attachment failed. SourcePath={SourcePath} ImageSource={ImageSource}", e.SourcePath, e.ImageSource);
                e.Complete(ReadAttachmentResult.Fail(ex.Message));
            });
    }

    private async Task EditorView_StoreEditorBlobRequested(object? sender, StoreEditorBlobRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            () => viewModel.StoreEditorBlobAsync(
                e.SourcePath,
                e.BlobId,
                e.ContentType,
                e.ContentBase64),
            () => e.Complete(EditorBlobResult.Success()),
            ex =>
            {
                logger?.LogWarning(
                    ex,
                    "Editor store blob failed. SourcePath={SourcePath} BlobId={BlobId}",
                    e.SourcePath,
                    e.BlobId);
                e.Complete(EditorBlobResult.Fail(ex.Message));
            });
    }

    private async Task EditorView_ReadEditorBlobRequested(object? sender, ReadEditorBlobRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            () => viewModel.ReadEditorBlobAsync(e.SourcePath, e.BlobId),
            e.Complete,
            ex =>
            {
                logger?.LogWarning(
                    ex,
                    "Editor read blob failed. SourcePath={SourcePath} BlobId={BlobId}",
                    e.SourcePath,
                    e.BlobId);
                e.Complete(EditorBlobReadResult.Fail(ex.Message));
            });
    }

    private Task EditorView_ListEditorBlobsRequested(object? sender, ListEditorBlobsRequestedEventArgs e)
    {
        EditorHostRequestSupport.Invoke(
            () => EditorBlobListResult.Success(viewModel.ListEditorBlobIds(e.SourcePath)),
            e.Complete,
            ex =>
            {
                logger?.LogWarning(ex, "Editor list blobs failed. SourcePath={SourcePath}", e.SourcePath);
                e.Complete(EditorBlobListResult.Fail(ex.Message));
            });

        return Task.CompletedTask;
    }
}
