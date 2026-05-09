using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.Shell.Editor;

internal sealed class EditorRequestController
{
    private readonly EditorWorkflowCoordinator editorWorkflow;
    private readonly IShellDialogService dialogs;

    internal EditorRequestController(
        EditorWorkflowCoordinator editorWorkflow,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(editorWorkflow);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.editorWorkflow = editorWorkflow;
        this.dialogs = dialogs;
    }

    internal async Task HandleClearEditorRequestedAsync()
    {
        await EditorShellOperationGuard.RunWithWarningAsync(
            editorWorkflow.ShowEmptyEditorViewAsync,
            dialogs,
            "Editor Not Ready",
            "The editor could not be cleared");
    }

    internal async Task HandleOpenNoteRequestedAsync(OpenNoteRequestedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        await EditorShellOperationGuard.RunWithWarningAsync(
            () => editorWorkflow.OpenNoteAsync(e.NotePath, e.SelectInTree),
            dialogs,
            "Open Note Failed",
            "The note could not be opened");
    }

    internal void HandleMentionTargetsChanged()
    {
        editorWorkflow.QueueMentionTargetsRefresh();
        editorWorkflow.RefreshDocumentLinksFromCurrentContent();
    }

    internal void HandleUserMessageRequested(UserMessageRequestedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        dialogs.ShowWarning(e.Title, e.Message);
    }

    internal void HandleCurrentNoteTitleChanged(string title)
    {
        editorWorkflow.UpdateCurrentNoteTitle(title);
    }

    internal async Task HandleEditorReadOnlyChangedAsync(bool isReadOnly)
    {
        await EditorShellOperationGuard.RunWithWarningAsync(
            () => editorWorkflow.SetReadOnlyAsync(isReadOnly),
            dialogs,
            "Editor Mode",
            "The editor mode could not be changed");
    }
}
