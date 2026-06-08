using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private Task BlockSuiteBridge_StoreEditorBlobRequested(
        object? sender,
        StoreEditorBlobRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            StoreEditorBlobRequested,
            e,
            e.SourcePath,
            e.Complete,
            EditorBlobResult.Fail,
            "The editor blob source is not the current note file.",
            "No editor blob writer is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite store editor blob bridge handler failed. SourcePath={SourcePath} BlobId={BlobId}",
                e.SourcePath,
                e.BlobId));
    }

    private Task BlockSuiteBridge_ReadEditorBlobRequested(
        object? sender,
        ReadEditorBlobRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            ReadEditorBlobRequested,
            e,
            e.SourcePath,
            e.Complete,
            EditorBlobReadResult.Fail,
            "The editor blob source is not the current note file.",
            "No editor blob reader is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite read editor blob bridge handler failed. SourcePath={SourcePath} BlobId={BlobId}",
                e.SourcePath,
                e.BlobId));
    }

    private Task BlockSuiteBridge_ListEditorBlobsRequested(
        object? sender,
        ListEditorBlobsRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            ListEditorBlobsRequested,
            e,
            e.SourcePath,
            e.Complete,
            EditorBlobListResult.Fail,
            "The editor blob source is not the current note file.",
            "No editor blob lister is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite list editor blobs bridge handler failed. SourcePath={SourcePath}",
                e.SourcePath));
    }
}
