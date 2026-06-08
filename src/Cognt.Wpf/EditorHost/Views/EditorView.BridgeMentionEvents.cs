using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private void BlockSuiteBridge_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        if (!BlockSuiteBridgeDispatch.TryHandleCurrentSourcePath(
                suppressBridgeCommands,
                IsCurrentSourcePath,
                e.SourcePath,
                () => e.Complete([])))
        {
            return;
        }

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
            MentionSearchRequested,
            e,
            onMissing: () => e.Complete([]),
            onFailure: ex =>
            {
                logger.LogWarning(ex, "BlockSuite mention search failed.");
                e.Complete([]);
            });
    }

    private void BlockSuiteBridge_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        if (!BlockSuiteBridgeDispatch.TryHandleCurrentSourcePath(
                suppressBridgeCommands,
                IsCurrentSourcePath,
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

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
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
        if (!BlockSuiteBridgeDispatch.TryHandleCurrentSourcePath(
                suppressBridgeCommands,
                IsCurrentSourcePath,
                e.SourcePath,
                () => PostMentionOpenResult(ok: false, error: "The mention source is not the current note file.")))
        {
            return;
        }

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
            OpenMentionRequested,
            e,
            beforeInvoke: () => currentContent = e.Content,
            onMissing: () => PostMentionOpenResult(ok: false, error: "No mention open handler is registered."),
            onFailure: ex => PostMentionOpenResult(ok: false, error: ex.Message));
    }
}
