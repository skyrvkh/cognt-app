using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
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
            logger.LogError(ex, "BlockSuite content change handling failed.");
            PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
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

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
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

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
            OpenImageOcclusionRequested,
            e,
            beforeInvoke: () => currentContent = e.Content,
            onMissing: () => PostSaveResult(ok: false, error: "No image occlusion handler is registered."),
            onFailure: ex => PostSaveResult(ok: false, error: ex.Message));
    }
}
