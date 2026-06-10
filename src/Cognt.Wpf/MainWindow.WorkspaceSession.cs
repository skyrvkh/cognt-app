using System.IO;
using System.Windows.Threading;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.Shell.Workspace;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private async Task RestoreWorkspaceSessionForCurrentCollectionAsync()
    {
        if (isRestoringWorkspaceSession || !viewModel.HasOpenCollection)
        {
            return;
        }

        string collectionPath = viewModel.OpenCollectionPath;
        WorkspaceSessionSnapshot snapshot = workspaceSessionStore.Load(collectionPath);
        if (snapshot.OpenNotes.Count == 0)
        {
            return;
        }

        isRestoringWorkspaceSession = true;
        isWorkspaceSessionRestoreInterrupted = false;
        try
        {
            overlayNavigation.HideOverlay();

            string? activeNotePath = snapshot.ActiveNotePath;
            NoteWorkspaceDocumentSession? lastRestoredSession = null;
            foreach (WorkspaceSessionNote note in snapshot.OpenNotes)
            {
                if (isWorkspaceSessionRestoreInterrupted)
                {
                    break;
                }

                if (!File.Exists(note.NotePath))
                {
                    continue;
                }

                if (note.Caret is not null)
                {
                    pendingNoteCaretRestoreStates[note.NotePath] = note.Caret;
                }

                bool shouldSelectInTree = IsSamePath(note.NotePath, activeNotePath);
                await QueueRestoreOpenNoteDocumentAsync(note.NotePath, shouldSelectInTree);
                if (openNoteDocuments.TryGetValue(note.NotePath, out NoteWorkspaceDocumentSession? restoredSession))
                {
                    lastRestoredSession = restoredSession;
                }
            }

            if (!string.IsNullOrWhiteSpace(activeNotePath)
                && openNoteDocuments.TryGetValue(activeNotePath, out NoteWorkspaceDocumentSession? activeSession))
            {
                ActivateNoteDocument(activeSession, selectInTree: true);
                activeSession.Focus();
            }
            else if (lastRestoredSession is not null)
            {
                ActivateNoteDocument(lastRestoredSession, selectInTree: true);
                lastRestoredSession.Focus();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workspace session restore failed. CollectionPath={CollectionPath}", collectionPath);
        }
        finally
        {
            isRestoringWorkspaceSession = false;
            isWorkspaceSessionRestoreInterrupted = false;
            QueueWorkspaceSessionSave();
        }
    }

    private void QueueWorkspaceSessionSave()
    {
        if (isRestoringWorkspaceSession
            || isSavingWorkspaceSession
            || isWorkspaceSessionSaveQueued
            || !viewModel.HasOpenCollection)
        {
            return;
        }

        isWorkspaceSessionSaveQueued = true;
        _ = Dispatcher.BeginInvoke(
            async () =>
            {
                isWorkspaceSessionSaveQueued = false;
                await SaveWorkspaceSessionAsync();
            },
            DispatcherPriority.Background);
    }

    private async Task SaveWorkspaceSessionAsync()
    {
        if (isSavingWorkspaceSession || string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath))
        {
            return;
        }

        isSavingWorkspaceSession = true;
        try
        {
            string collectionPath = viewModel.OpenCollectionPath;
            WorkspaceSessionSnapshot snapshot = await CreateWorkspaceSessionSnapshotAsync(collectionPath);
            workspaceSessionStore.Save(snapshot);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workspace session save failed.");
        }
        finally
        {
            isSavingWorkspaceSession = false;
        }
    }

    private async Task<WorkspaceSessionSnapshot> CreateWorkspaceSessionSnapshotAsync(string collectionPath)
    {
        List<WorkspaceSessionNote> notes = [];
        NoteWorkspaceDocumentSession[] sessions = openNoteDocuments.Values
            .Where(candidate => !candidate.IsClosed)
            .ToArray();
        foreach (NoteWorkspaceDocumentSession session in sessions)
        {
            EditorCaretState? caret = await session.GetCaretStateAsync();
            notes.Add(new WorkspaceSessionNote
            {
                NotePath = session.NotePath,
                Caret = caret,
            });
        }

        string? activeNotePath = GetActiveNoteDocument()?.NotePath;
        if (!notes.Any(note => IsSamePath(note.NotePath, activeNotePath)))
        {
            activeNotePath = notes.LastOrDefault()?.NotePath;
        }

        return new WorkspaceSessionSnapshot
        {
            CollectionPath = collectionPath,
            ActiveNotePath = activeNotePath,
            OpenNotes = notes,
        };
    }

    private EditorCaretState? TakePendingCaretRestoreState(string notePath)
    {
        if (!pendingNoteCaretRestoreStates.TryGetValue(notePath, out EditorCaretState? caret))
        {
            return null;
        }

        pendingNoteCaretRestoreStates.Remove(notePath);
        return caret;
    }

    private static bool IsSamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
