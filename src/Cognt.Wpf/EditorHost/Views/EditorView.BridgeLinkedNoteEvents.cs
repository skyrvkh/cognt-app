using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private void BlockSuiteBridge_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        if (!BlockSuiteBridgeDispatch.TryHandleCurrentSourcePath(
                suppressBridgeCommands,
                IsCurrentSourcePath,
                e.SourcePath,
                () => e.Complete(CreateLinkedNoteResult.Fail("The linked note source is not the current note file."))))
        {
            return;
        }

        BlockSuiteBridgeDispatch.TryInvokeHandler(
            this,
            CreateLinkedNoteRequested,
            e,
            onMissing: () => e.Complete(CreateLinkedNoteResult.Fail("No linked note handler is registered.")),
            onFailure: ex => e.Complete(CreateLinkedNoteResult.Fail(ex.Message)));
    }
}
