using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal void SetActiveWorkspaceNote(
        string notePath,
        string title,
        string content,
        long? itemId = null,
        string? savedContent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notePath);
        ArgumentNullException.ThrowIfNull(content);

        currentPdfDocument = null;
        currentNotePath = Path.GetFullPath(notePath);
        currentNoteItemId = itemId;
        currentNoteTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(notePath)
            : title.Trim();
        lastSavedContent = savedContent ?? content;
        LoadNoteToCardTagsForCurrentNote();
        OnPropertyChanged(nameof(CurrentNotePath));
        OnPropertyChanged(nameof(CurrentNoteRelativePath));
        RefreshEditorBreadcrumb();
        ReloadCurrentNoteToCardQueue(GetRequiredRelativeNotePath(currentNotePath), content);
        CurrentNoteContentChanged?.Invoke(this, content);
    }

    public FlashcardSourceContext GetFlashcardSourceContext()
    {
        if (currentPdfDocument is not null)
        {
            return new FlashcardSourceContext(
                currentPdfDocument.RelativePath,
                string.IsNullOrWhiteSpace(currentPdfDocument.Title)
                    ? Path.GetFileName(currentPdfDocument.RelativePath)
                    : currentPdfDocument.Title);
        }

        if (string.IsNullOrWhiteSpace(openCollectionPath)
            || string.IsNullOrWhiteSpace(currentNotePath))
        {
            return new FlashcardSourceContext(null, "No source");
        }

        string sourceRelPath = GetRequiredCurrentNoteRelativePath();
        string sourceLabel = string.IsNullOrWhiteSpace(currentNoteTitle)
            ? Path.GetFileNameWithoutExtension(currentNotePath)
            : currentNoteTitle;

        return new FlashcardSourceContext(sourceRelPath, sourceLabel);
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        if (parameter is string notePath && !string.IsNullOrWhiteSpace(notePath))
        {
            RequestOpenResolvedNotePath(notePath, selectInTree: true);
        }
    }

    private void RequestOpenResolvedNotePath(string notePath, bool selectInTree)
    {
        OpenNoteRequested?.Invoke(this, new Navigation.OpenNoteRequestedEventArgs(notePath, SelectInTree: selectInTree));
    }

    internal void OpenNoteByRelativePath(string relativePath, bool selectInTree = true)
    {
        OpenNoteByRelativePath(openCollectionPath, relativePath, selectInTree);
    }

    internal void OpenNoteByRelativePath(string collectionPath, string relativePath, bool selectInTree = true)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || !Directory.Exists(collectionPath))
        {
            throw new InvalidOperationException("Open a valid collection before opening a note.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("A relative note path is required.");
        }

        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string notePath = ResolveCollectionFilePath(collectionPath, normalizedRelativePath);
        if (!File.Exists(notePath))
        {
            throw new InvalidOperationException("The selected note file could not be found in this collection.");
        }

        if (!AreSamePath(collectionPath, CurrentCollectionPath))
        {
            LoadCollection(collectionPath);
        }

        RequestOpenResolvedNotePath(notePath, selectInTree);
    }

    internal string ResolveMentionPath(string relativePath)
    {
        return noteService.ResolveMentionPath(openCollectionPath, relativePath);
    }
}
