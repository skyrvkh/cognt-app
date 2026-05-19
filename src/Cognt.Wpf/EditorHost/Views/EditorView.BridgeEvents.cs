using System.IO;
using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private void BlockSuiteBridge_DocumentChanged(object? sender, EventArgs e)
    {
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BlockSuiteBridge_ContentChanged(object? sender, string content)
    {
        try
        {
            if (transientContentChanged is not null)
            {
                transientContentChanged(content);
                return;
            }

            currentContent = content;
            ContentChanged?.Invoke(this, currentContent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite content change handling failed: {ex}");
            PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        TryInvokeHandler(
            CreateExtractRequested,
            e,
            onMissing: () => PostExtractResult(ok: false, error: "No extract handler is registered."),
            onFailure: ex => PostExtractResult(ok: false, error: ex.Message));
    }

    private void BlockSuiteBridge_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            PostSaveResult(ok: false, error: "The cloze source is not the current note file.");
            return;
        }

        TryInvokeHandler(
            SaveClozeRequested,
            e,
            beforeInvoke: () => currentContent = e.Content,
            onMissing: () => PostSaveResult(ok: false, error: "No cloze draft handler is registered."),
            onFailure: ex => PostSaveResult(ok: false, error: ex.Message));
    }

    private void BlockSuiteBridge_OpenImageOcclusionRequested(object? sender, OpenImageOcclusionRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            PostSaveResult(ok: false, error: "The image occlusion source is not the current note file.");
            return;
        }

        TryInvokeHandler(
            OpenImageOcclusionRequested,
            e,
            beforeInvoke: () => currentContent = e.Content,
            onMissing: () => PostSaveResult(ok: false, error: "No image occlusion handler is registered."),
            onFailure: ex => PostSaveResult(ok: false, error: ex.Message));
    }

    private void BlockSuiteBridge_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(e.SourcePath, () => e.Complete([])))
        {
            return;
        }

        TryInvokeHandler(
            MentionSearchRequested,
            e,
            onMissing: () => e.Complete([]),
            onFailure: ex =>
            {
                System.Diagnostics.Debug.WriteLine($"BlockSuite mention search failed: {ex}");
                e.Complete([]);
            });
    }

    private void BlockSuiteBridge_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(
                e.SourcePath,
                () => PostMentionPreviewResult(
                    e.RequestId,
                    e.RelativePath,
                    e.Mode,
                    ok: false,
                    error: "The mention preview source is not the current note file.")))
        {
            return;
        }

        TryInvokeHandler(
            MentionPreviewRequested,
            e,
            onMissing: () => PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: "No mention preview handler is registered."),
            onFailure: ex => PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: ex.Message));
    }

    private void BlockSuiteBridge_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(
                e.SourcePath,
                () => PostMentionOpenResult(ok: false, error: "The mention source is not the current note file.")))
        {
            return;
        }

        TryInvokeHandler(
            OpenMentionRequested,
            e,
            beforeInvoke: () => currentContent = e.Content,
            onMissing: () => PostMentionOpenResult(ok: false, error: "No mention open handler is registered."),
            onFailure: ex => PostMentionOpenResult(ok: false, error: ex.Message));
    }

    private void BlockSuiteBridge_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(
                e.SourcePath,
                () => e.Complete(CreateLinkedNoteResult.Fail("The linked note source is not the current note file."))))
        {
            return;
        }

        TryInvokeHandler(
            CreateLinkedNoteRequested,
            e,
            onMissing: () => e.Complete(CreateLinkedNoteResult.Fail("No linked note handler is registered.")),
            onFailure: ex => e.Complete(CreateLinkedNoteResult.Fail(ex.Message)));
    }

    private async Task BlockSuiteBridge_AttachFileRequested(object? sender, AttachFileRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(
                e.SourcePath,
                () => e.Complete(AttachFileResult.Fail("The attachment source is not the current note file."))))
        {
            return;
        }

        if (AttachFileRequested is null)
        {
            e.Complete(AttachFileResult.Fail("No attachment handler is registered."));
            return;
        }

        try
        {
            await AttachFileRequested.Invoke(this, e);
        }
        catch (Exception ex)
        {
            e.Complete(AttachFileResult.Fail(ex.Message));
        }
    }

    private async Task BlockSuiteBridge_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        if (!TryHandleCurrentSourcePath(
                e.SourcePath,
                () => e.Complete(ReadAttachmentResult.Fail("The attachment source is not the current note file."))))
        {
            return;
        }

        if (ReadAttachmentRequested is null)
        {
            e.Complete(ReadAttachmentResult.Fail("No attachment reader is registered."));
            return;
        }

        try
        {
            await ReadAttachmentRequested.Invoke(this, e);
        }
        catch (Exception ex)
        {
            e.Complete(ReadAttachmentResult.Fail(ex.Message));
        }
    }

    private void BlockSuiteBridge_DiagnosticReceived(object? sender, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            logger.LogDebug("BlockSuite host diagnostic: {Message}", message);
            System.Diagnostics.Debug.WriteLine($"BlockSuite host: {message}");
        }
    }
}
