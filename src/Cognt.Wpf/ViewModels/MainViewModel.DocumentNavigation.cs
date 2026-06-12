using System.IO;
using System.Diagnostics;
using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private const int LargeDocumentSaveLogThreshold = 200_000;

    internal Navigation.PreparedNote? PrepareNoteForOpen(string notePath, bool forceRead = false)
    {
        if (!forceRead && string.Equals(currentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SaveCurrentNoteFromEditor();
        string fullPath = Path.GetFullPath(notePath);
        string relativePath = GetRequiredRelativeNotePath(fullPath);
        KnowledgeOpenedNoteReadModel openedNote = noteService.ReadNoteForOpen(openCollectionPath, relativePath);

        return new Navigation.PreparedNote(
            fullPath,
            openedNote.Title,
            openedNote.Content,
            openedNote.ItemId);
    }

    internal void CompleteOpenNote(Navigation.PreparedNote note)
    {
        SetActiveWorkspaceNote(note.NotePath, note.Title, note.Content, note.ItemId);
    }

    public void ClearCurrentNote()
    {
        SaveCurrentNoteFromEditor();
        ClearCurrentNoteState();
        ClearEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SaveCurrentNoteFromEditor()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        string? editorNotePath = GetCurrentEditorNotePath?.Invoke();
        if (GetCurrentEditorNotePath is not null
            && (string.IsNullOrWhiteSpace(editorNotePath)
                || !AreSamePath(editorNotePath, currentNotePath)))
        {
            logger?.LogWarning(
                "Skipped current note save because active editor path does not match current note. CurrentNotePath={CurrentNotePath} EditorNotePath={EditorNotePath}",
                currentNotePath,
                editorNotePath);
            return;
        }

        SaveNoteIfChanged(GetCurrentEditorContent?.Invoke() ?? string.Empty);
    }

    public void SaveNoteIfChanged(string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return;
        }

        SaveNoteIfChanged(currentNotePath, content);
    }

    public void SaveNoteIfChanged(string notePath, string content)
    {
        NoteSaveRequest? request = NoteSaveRequestBuilder.Create(
            notePath,
            content,
            openCollectionPath,
            currentNotePath,
            lastSavedContent,
            GetRequiredRelativeNotePath,
            LargeDocumentSaveLogThreshold);
        if (request is null)
        {
            return;
        }

        noteSaveSemaphore.Wait();
        try
        {
            noteService.SaveNote(request.CollectionPath, request.RelativePath, request.Content);
        }
        finally
        {
            noteSaveSemaphore.Release();
        }

        MarkSavedNote(request);
    }

    public Task SaveNoteIfChangedAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return SaveNoteIfChangedAsync(currentNotePath, content);
    }

    public Task SaveNoteIfChangedAsync(string notePath, string content)
    {
        NoteSaveRequest? request = NoteSaveRequestBuilder.Create(
            notePath,
            content,
            openCollectionPath,
            currentNotePath,
            lastSavedContent,
            GetRequiredRelativeNotePath,
            LargeDocumentSaveLogThreshold);
        if (request is null)
        {
            return Task.CompletedTask;
        }

        if (request.ShouldLogLargeSave)
        {
            logger?.LogInformation(
                "Large note save queued. RelativePath={RelativePath} ContentLength={ContentLength}",
                request.RelativePath,
                request.Content.Length);
        }

        return Task.Run(async () =>
        {
            await noteSaveSemaphore.WaitAsync().ConfigureAwait(false);
            Stopwatch? stopwatch = request.ShouldLogLargeSave ? Stopwatch.StartNew() : null;
            try
            {
                noteService.SaveNote(request.CollectionPath, request.RelativePath, request.Content);
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    logger?.LogInformation(
                        "Large note save completed. RelativePath={RelativePath} ElapsedMs={ElapsedMs}",
                        request.RelativePath,
                        stopwatch.ElapsedMilliseconds);
                }

                noteSaveSemaphore.Release();
            }

            await InvokeOnUiThreadAsync(() =>
            {
                MarkSavedNote(request);
            }).ConfigureAwait(false);
        });
    }

    private void MarkSavedNote(NoteSaveRequest request)
        => MarkSavedNote(
            request.FullNotePath,
            request.CollectionPath,
            request.RelativePath,
            request.Content);

    private void MarkSavedNote(
        string notePath,
        string collectionPath,
        string relativePath,
        string content)
    {
        if (!NoteSaveRequestBuilder.IsCurrentSavedNote(
            currentNotePath,
            openCollectionPath,
            notePath,
            collectionPath))
        {
            return;
        }

        lastSavedContent = content;
        ScheduleCurrentNoteToCardQueueRefresh(relativePath, content);
        CurrentNoteContentChanged?.Invoke(this, content);
    }

    private static Task InvokeOnUiThreadAsync(Action action)
    {
        if (Application.Current?.Dispatcher is not { } dispatcher
            || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public void SelectTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null || node.IsPlaceholder)
        {
            return;
        }

        if (node.FlashcardCardId is long cardId)
        {
            OpenFlashcardRequested?.Invoke(
                this,
                new Navigation.OpenFlashcardRequestedEventArgs(
                    cardId,
                    node.FileRelPath,
                    node.Title));
            return;
        }

        if (node.Kind == KnowledgeItemKind.PdfDocument)
        {
            OpenPdfDocumentFromTreeNode(node);
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.NotePath))
        {
            currentNoteItemId = node.Id;
            RequestOpenResolvedNotePath(node.NotePath, selectInTree: false);
            return;
        }
    }

    private void SelectFolderNode(NoteTreeNodeViewModel node)
    {
        SaveCurrentNoteFromEditor();

        bool isSameFolder = SelectCurrentFolder(node.Id, node.Title);
        if (!isSameFolder)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectFolderItem(KnowledgeTreeItemReadModel item)
    {
        SaveCurrentNoteFromEditor();

        bool isSameFolder = SelectCurrentFolder(item.Id, item.Title);
        if (!isSameFolder)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool SelectCurrentFolder(long folderId, string title)
    {
        bool isSameFolder = currentNoteItemId == folderId && string.IsNullOrWhiteSpace(currentNotePath);
        currentNotePath = null;
        currentNoteItemId = folderId;
        currentNoteTitle = title;
        lastSavedContent = string.Empty;
        OnPropertyChanged(nameof(CurrentNotePath));
        OnPropertyChanged(nameof(CurrentNoteRelativePath));
        RefreshEditorBreadcrumb();
        CurrentNoteToCardQueue = null;
        return isSameFolder;
    }

    public bool NavigateToBreadcrumb(Navigation.EditorBreadcrumbItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsCurrent)
        {
            return false;
        }

        KnowledgeTreeItemReadModel treeItem = noteService.GetItem(openCollectionPath, item.Id)
            ?? throw new InvalidOperationException("This breadcrumb item no longer exists.");

        if (treeItem.Kind == KnowledgeItemKind.Note)
        {
            string notePath = item.NotePath ?? ResolveNotePath(treeItem)
                ?? throw new InvalidOperationException("The breadcrumb note file could not be resolved.");
            currentNoteItemId = item.Id;
            RequestOpenResolvedNotePath(notePath, selectInTree: true);
            return true;
        }

        SelectFolderItem(treeItem);
        return true;
    }
}
