using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;

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
        await editorRequests.HandleClearEditorRequestedAsync();
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        await editorRequests.HandleOpenNoteRequestedAsync(e);
    }

    private void ViewModel_MentionTargetsChanged(object? sender, EventArgs e)
    {
        editorRequests.HandleMentionTargetsChanged();
    }

    private void ViewModel_UserMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        editorRequests.HandleUserMessageRequested(e);
    }

    private void ViewModel_CurrentNoteTitleChanged(object? sender, string title)
    {
        editorRequests.HandleCurrentNoteTitleChanged(title);
    }

    private async void ViewModel_EditorReadOnlyChanged(object? sender, bool isReadOnly)
    {
        await editorRequests.HandleEditorReadOnlyChangedAsync(isReadOnly);
    }

    private async void AllNotesViewModel_OpenNoteRequested(object? sender, OpenAllNotesRequestedEventArgs e)
    {
        await overlayNavigation.HandleAllNotesOpenRequestedAsync(e);
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
