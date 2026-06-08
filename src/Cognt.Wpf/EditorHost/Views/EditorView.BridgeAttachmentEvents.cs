using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private Task BlockSuiteBridge_AttachFileRequested(object? sender, AttachFileRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            AttachFileRequested,
            e,
            e.SourcePath,
            e.Complete,
            AttachFileResult.Fail,
            "The attachment source is not the current note file.",
            "No attachment handler is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite attach-file bridge handler failed. SourcePath={SourcePath} FileName={FileName}",
                e.SourcePath,
                e.FileName));
    }

    private Task BlockSuiteBridge_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            ReadAttachmentRequested,
            e,
            e.SourcePath,
            e.Complete,
            ReadAttachmentResult.Fail,
            "The attachment source is not the current note file.",
            "No attachment reader is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite read-attachment bridge handler failed. SourcePath={SourcePath} ImageSource={ImageSource}",
                e.SourcePath,
                e.ImageSource));
    }

    private Task BlockSuiteBridge_AttachImageSourceRequested(
        object? sender,
        AttachImageSourceRequestedEventArgs e)
    {
        return DispatchCurrentSourceAsyncBridgeRequest(
            AttachImageSourceRequested,
            e,
            e.SourcePath,
            e.Complete,
            AttachFileResult.Fail,
            "The image source attachment is not for the current note file.",
            "No image source attachment handler is registered.",
            ex => logger.LogWarning(
                ex,
                "BlockSuite attach-image-source bridge handler failed. SourcePath={SourcePath} ImageSource={ImageSource}",
                e.SourcePath,
                e.ImageSource));
    }
}
