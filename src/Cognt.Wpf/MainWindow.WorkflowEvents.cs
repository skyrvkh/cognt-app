using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Shell.Workspace;
using CogntApp.Views;
using Microsoft.Extensions.Logging;
using System.Windows.Threading;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OverlayRequested(object? sender, OverlayRequestedEventArgs e)
    {
        try
        {
            await overlayNavigation.HandleOverlayRequestedAsync(e);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Overlay request failed. ViewKind={ViewKind}", e.ViewKind);
            shellDialogs.ShowWarning("Navigation Failed", $"The requested view could not be opened: {ex.Message}");
        }
    }

    private async void DecksView_PracticeRequested(object? sender, EventArgs e)
    {
        try
        {
            await overlayNavigation.HandlePracticeRequestedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Practice review request failed.");
            shellDialogs.ShowWarning("Practice Not Ready", $"Practice mode could not start: {ex.Message}");
        }
    }

    private async void DecksView_CramRequested(object? sender, CramRequestedEventArgs e)
    {
        try
        {
            await overlayNavigation.HandleCramRequestedAsync(e.Scope, e.Settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cram review request failed. Scope={Scope}", e.Scope);
            shellDialogs.ShowWarning("Cram Not Ready", $"Cram mode could not start: {ex.Message}");
        }
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
        try
        {
            await overlayNavigation.HandleReviewExitRequestedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review exit failed.");
            shellDialogs.ShowWarning("Review Exit Failed", $"The review session could not close cleanly: {ex.Message}");
        }
    }

    private async void ViewModel_ClearEditorRequested(object? sender, EventArgs e)
    {
        try
        {
            await CloseActiveNoteDocumentAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Clear editor request failed.");
            shellDialogs.ShowWarning("Close Note Failed", $"The active note could not be closed: {ex.Message}");
        }
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        try
        {
            overlayNavigation.HideOverlay();
            await QueueOpenNoteDocumentAsync(e.NotePath, e.SelectInTree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Open note request failed. NotePath={NotePath}", e.NotePath);
            shellDialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
        }
    }

    private async void ViewModel_OpenFlashcardRequested(object? sender, OpenFlashcardRequestedEventArgs e)
    {
        try
        {
            await OpenFlashcardDocumentAsync(e);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Open flashcard request failed. CardId={CardId}", e.CardId);
            shellDialogs.ShowWarning("Open Card Failed", $"The card could not be opened: {ex.Message}");
        }
    }

    private Task ViewModel_NoteDocumentsDeleting(object? sender, NoteDocumentsDeletedEventArgs e)
    {
        return CloseDeletedNoteDocumentsAsync(e.NotePaths);
    }

    private async void ViewModel_NoteDocumentsDeleted(object? sender, NoteDocumentsDeletedEventArgs e)
    {
        try
        {
            await CloseDeletedNoteDocumentsAsync(e.NotePaths);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Close deleted note documents failed after delete notification.");
        }
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
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        session?.UpdateTitle(title);
    }

    private void ViewModel_CurrentNoteContentChanged(object? sender, string content)
    {
        noteInspectorViewModel.UpdateCurrentContent(content);
    }

    private async void ViewModel_CurrentNoteEditorContentReplacementRequested(object? sender, string content)
    {
        try
        {
            NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
            if (session is null)
            {
                return;
            }

            await session.ReplaceContentAsync(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Current note editor content replacement failed.");
            shellDialogs.ShowWarning("Editor Refresh Failed", $"The generated-card cleanup was saved, but the editor could not reload it: {ex.Message}");
        }
        finally
        {
            AutoHideNoteCardQueuePane();
        }
    }

    private void AutoHideNoteCardQueuePane()
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            EnforceRightPaneTabPolicy();
            EnsureToolAutoHidden(CardsAnchorable);
        }, DispatcherPriority.Background);
    }

    private async void ViewModel_EditorReadOnlyChanged(object? sender, bool isReadOnly)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is not null)
        {
            await session.SetReadOnlyAsync(isReadOnly);
        }
        else
        {
        }
    }

    private async void NoteInspectorViewModel_OutlineNavigationRequested(
        object? sender,
        NoteOutlineNavigationRequestedEventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.FocusBlockAsync(e.BlockId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outline navigation failed. NotePath={NotePath} BlockId={BlockId}", session.NotePath, e.BlockId);
            shellDialogs.ShowWarning("Outline", $"The heading could not be opened: {ex.Message}");
        }
    }

    private void NoteDocumentSession_CurrentOutlineBlockChanged(
        object? sender,
        NoteWorkspaceCurrentOutlineBlockChangedEventArgs e)
    {
        if (sender is NoteWorkspaceDocumentSession session
            && ReferenceEquals(session, GetActiveNoteDocument()))
        {
            noteInspectorViewModel.UpdateActiveOutlineBlock(e.NotePath, e.BlockId);
        }
    }

    private void AllNotesViewModel_OpenNoteRequested(object? sender, OpenAllNotesRequestedEventArgs e)
    {
        try
        {
            overlayNavigation.HideOverlay();
            viewModel.OpenNoteByRelativePath(e.RelativePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "All notes open note request failed. RelativePath={RelativePath}", e.RelativePath);
            shellDialogs.ShowWarning("Open Note Failed", $"The note could not be opened: {ex.Message}");
        }
    }

    private async void AllNotesViewModel_ShowCardsRequested(object? sender, OpenNoteCardsRequestedEventArgs e)
    {
        try
        {
            await overlayNavigation.HandleNoteCardsRequestedAsync(e);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "All notes show cards request failed. RelativePath={RelativePath}", e.RelativePath);
            shellDialogs.ShowWarning("Open Cards Failed", $"Cards for this note could not be opened: {ex.Message}");
        }
    }

    private void AllNotesViewModel_CreateNoteRequested(object? sender, EventArgs e)
    {
        if (viewModel.AddNoteCommand.CanExecute(null))
        {
            viewModel.AddNoteCommand.Execute(null);
            overlayNavigation.HideOverlay();
        }
        else
        {
        }
    }

    private void AllNotesViewModel_ImportDocumentsRequested(object? sender, EventArgs e)
    {
        attachmentWorkflow.ShowDocumentImport(this);
    }

}
