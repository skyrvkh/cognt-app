using CogntApp.Shell.Dialogs;
using CogntApp.Models.Navigation;

namespace CogntApp.Shell.Editor;

internal sealed class EditorRequestController
{
    private readonly Func<Task> showEmptyEditorViewAsync;
    private readonly Func<string, bool, Task> openNoteAsync;
    private readonly Action queueMentionTargetsRefresh;
    private readonly Action<string> updateCurrentNoteTitle;
    private readonly IShellDialogService dialogs;

    internal EditorRequestController(
        Func<Task> showEmptyEditorViewAsync,
        Func<string, bool, Task> openNoteAsync,
        Action queueMentionTargetsRefresh,
        Action<string> updateCurrentNoteTitle,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(showEmptyEditorViewAsync);
        ArgumentNullException.ThrowIfNull(openNoteAsync);
        ArgumentNullException.ThrowIfNull(queueMentionTargetsRefresh);
        ArgumentNullException.ThrowIfNull(updateCurrentNoteTitle);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.showEmptyEditorViewAsync = showEmptyEditorViewAsync;
        this.openNoteAsync = openNoteAsync;
        this.queueMentionTargetsRefresh = queueMentionTargetsRefresh;
        this.updateCurrentNoteTitle = updateCurrentNoteTitle;
        this.dialogs = dialogs;
    }

    internal async Task HandleClearEditorRequestedAsync()
    {
        await EditorShellOperationGuard.RunWithWarningAsync(
            showEmptyEditorViewAsync,
            dialogs,
            "Editor Not Ready",
            "The editor could not be cleared");
    }

    internal async Task HandleOpenNoteRequestedAsync(OpenNoteRequestedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        await EditorShellOperationGuard.RunWithWarningAsync(
            () => openNoteAsync(e.NotePath, e.SelectInTree),
            dialogs,
            "Open Note Failed",
            "The note could not be opened");
    }

    internal void HandleMentionTargetsChanged()
    {
        queueMentionTargetsRefresh();
    }

    internal void HandleUserMessageRequested(UserMessageRequestedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        dialogs.ShowWarning(e.Title, e.Message);
    }

    internal void HandleCurrentNoteTitleChanged(string title)
    {
        updateCurrentNoteTitle(title);
    }
}
