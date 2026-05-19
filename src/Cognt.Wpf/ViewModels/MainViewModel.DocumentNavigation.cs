using System.IO;
using System.Diagnostics;
using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
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
        currentPdfDocument = null;
        currentNotePath = note.NotePath;
        currentNoteItemId = note.ItemId;
        currentNoteTitle = note.Title;
        lastSavedContent = note.Content;
        ClearNoteToCardTags();
        OnPropertyChanged(nameof(CurrentNotePath));
        RefreshEditorBreadcrumb();
        ReloadCurrentNoteToCardQueue(GetRequiredRelativeNotePath(note.NotePath), note.Content);
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
        if (string.IsNullOrWhiteSpace(notePath)
            || string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return;
        }

        string fullNotePath = Path.GetFullPath(notePath);
        string collectionPath = openCollectionPath;
        string relativePath = GetRequiredRelativeNotePath(fullNotePath);
        if (IsCurrentSavedNote(fullNotePath, collectionPath)
            && string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return;
        }

        noteSaveSemaphore.Wait();
        try
        {
            noteService.SaveNote(collectionPath, relativePath, content);
        }
        finally
        {
            noteSaveSemaphore.Release();
        }

        MarkSavedNote(fullNotePath, collectionPath, relativePath, content);
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
        if (string.IsNullOrWhiteSpace(notePath)
            || string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return Task.CompletedTask;
        }

        string fullNotePath = Path.GetFullPath(notePath);
        string collectionPath = openCollectionPath;
        string relativePath = GetRequiredRelativeNotePath(fullNotePath);
        if (IsCurrentSavedNote(fullNotePath, collectionPath)
            && string.Equals(content, lastSavedContent, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        bool logLargeSave = content.Length >= LargeDocumentSaveLogThreshold;
        if (logLargeSave)
        {
            Debug.WriteLine($"Queued large note save for {relativePath}: {content.Length} chars.");
        }

        return Task.Run(async () =>
        {
            await noteSaveSemaphore.WaitAsync().ConfigureAwait(false);
            Stopwatch? stopwatch = logLargeSave ? Stopwatch.StartNew() : null;
            try
            {
                noteService.SaveNote(collectionPath, relativePath, content);
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    Debug.WriteLine($"Large note save write/index for {relativePath}: {stopwatch.ElapsedMilliseconds}ms.");
                }

                noteSaveSemaphore.Release();
            }

            await InvokeOnUiThreadAsync(() =>
            {
                MarkSavedNote(fullNotePath, collectionPath, relativePath, content);
            }).ConfigureAwait(false);
        });
    }

    private bool IsCurrentSavedNote(string notePath, string collectionPath)
    {
        return !string.IsNullOrWhiteSpace(currentNotePath)
            && string.Equals(Path.GetFullPath(currentNotePath), notePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(openCollectionPath, collectionPath, StringComparison.OrdinalIgnoreCase);
    }

    private void MarkSavedNote(
        string notePath,
        string collectionPath,
        string relativePath,
        string content)
    {
        if (!IsCurrentSavedNote(notePath, collectionPath))
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
            ClearCurrentNote();
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

        SelectFolderNode(node);
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
