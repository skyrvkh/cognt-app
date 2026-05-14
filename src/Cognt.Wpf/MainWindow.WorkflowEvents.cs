using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Shell.Workspace;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OverlayRequested(object? sender, OverlayRequestedEventArgs e)
    {
        await overlayNavigation.HandleOverlayRequestedAsync(e);
    }

    private async void DecksView_PracticeRequested(object? sender, EventArgs e)
    {
        await overlayNavigation.HandlePracticeRequestedAsync();
    }

    private async void DecksView_CreateCardEditorHostRequested(
        object? sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        await overlayNavigation.HandleCreateCardEditorHostRequestedAsync(e);
    }

    private async void DecksView_CreateCardEditorReleased(object? sender, EventArgs e)
    {
        await overlayNavigation.HandleCreateCardEditorReleasedAsync();
    }

    private void DecksView_InfoMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        shellDialogs.ShowInfo(e.Title, e.Message);
    }

    private void DecksView_WarningMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        shellDialogs.ShowWarning(e.Title, e.Message);
    }

    private async void ReviewView_ExitReviewRequested(object? sender, EventArgs e)
    {
        await overlayNavigation.HandleReviewExitRequestedAsync();
    }

    private async void ViewModel_ClearEditorRequested(object? sender, EventArgs e)
    {
        await CloseActiveNoteDocumentAsync();
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        overlayNavigation.HideOverlay();
        await OpenOrActivateNoteDocumentAsync(e.NotePath, e.SelectInTree);
    }

    private void ViewModel_MentionTargetsChanged(object? sender, EventArgs e)
    {
        RefreshOpenDocumentMentionTargets();
    }

    private void ViewModel_UserMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        shellDialogs.ShowWarning(e.Title, e.Message);
    }

    private void ViewModel_CurrentNoteTitleChanged(object? sender, string title)
    {
        GetActiveNoteDocument()?.UpdateTitle(title);
    }

    private async void ViewModel_EditorReadOnlyChanged(object? sender, bool isReadOnly)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is not null)
        {
            await session.SetReadOnlyAsync(isReadOnly);
        }
    }

    private async void AllNotesViewModel_OpenNoteRequested(object? sender, OpenAllNotesRequestedEventArgs e)
    {
        try
        {
            overlayNavigation.HideOverlay();
            viewModel.OpenNoteByRelativePath(e.RelativePath);
        }
        catch (Exception ex)
        {
            shellDialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
        }
    }

    private void AllNotesViewModel_CreateNoteRequested(object? sender, EventArgs e)
    {
        if (viewModel.AddNoteCommand.CanExecute(null))
        {
            viewModel.AddNoteCommand.Execute(null);
            overlayNavigation.HideOverlay();
        }
    }

    private void AllNotesViewModel_ImportDocumentsRequested(object? sender, EventArgs e)
    {
        attachmentWorkflow.ShowDocumentImport(this);
    }

}
