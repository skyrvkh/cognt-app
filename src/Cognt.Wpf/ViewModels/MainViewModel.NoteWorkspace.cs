using System.IO;
using CogntApp.Models.Navigation;
using CogntApp.Models.Tree;
using KnowledgeOpenedNoteReadModel = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.KnowledgeOpenedNoteReadModel;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void LoadNotesDirectory(string directoryPath, bool clearEditor = true)
    {
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath.Trim()));
        }
        catch (Exception ex)
        {
            RaiseMessage("Invalid Collection", $"The collection path is invalid: {ex.Message}");
            return;
        }

        if (!collectionPathResolver.IsCollectionPath(fullPath))
        {
            RaiseMessage(
                "Invalid Collection",
                $"Only collection folders ending with \"{collectionPathResolver.CollectionDirectoryExtension}\" can be opened.");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            RaiseMessage("Collection Not Found", $"The collection folder does not exist:{Environment.NewLine}{fullPath}");
            return;
        }

        bool isSwitchingCollection =
            !string.IsNullOrWhiteSpace(currentNotesDirectory)
            && !string.Equals(currentNotesDirectory, fullPath, StringComparison.OrdinalIgnoreCase);

        if (isSwitchingCollection)
        {
            SaveCurrentNoteFromEditor();
            noteService.CloseCurrentCollection();
        }

        currentNotesDirectory = fullPath;
        NotesDirectory = currentNotesDirectory;
        OnPropertyChanged(nameof(OpenCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionPath));
        ClearCurrentNoteState();
        RefreshNotesTree(preserveExpansion: true);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (clearEditor)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public PreparedNote? PrepareNoteForOpen(string notePath)
    {
        if (string.Equals(currentNotePath, notePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SaveCurrentNoteFromEditor();
        string fullPath = Path.GetFullPath(notePath);
        string content = noteService.ReadNoteFile(fullPath);
        TreeItemRow? noteItem = string.IsNullOrWhiteSpace(currentNotesDirectory)
            ? null
            : noteService.FindNoteByPath(currentNotesDirectory, fullPath);

        KnowledgeOpenedNoteReadModel openedNote = new(
            string.IsNullOrWhiteSpace(currentNotesDirectory)
                ? fullPath
                : Path.GetRelativePath(currentNotesDirectory, fullPath),
            noteItem?.Title ?? Path.GetFileNameWithoutExtension(fullPath),
            content,
            noteItem?.Id);

        return new PreparedNote(
            fullPath,
            openedNote.Title,
            openedNote.Content,
            openedNote.ItemId);
    }

    public void LoadCollection(string directoryPath, bool clearEditor = true)
    {
        LoadNotesDirectory(directoryPath, clearEditor);
    }

    public void CompleteOpenNote(PreparedNote note)
    {
        currentNotePath = note.NotePath;
        currentNoteItemId = note.ItemId;
        currentNoteTitle = note.Title;
        lastSavedContent = note.Content;
        OnPropertyChanged(nameof(CurrentNotePath));
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

        noteService.SaveNote(currentNotePath, content);
        lastSavedContent = content;
    }

    public void SelectTreeNode(NoteTreeNodeViewModel? node)
    {
        if (node is null || node.IsPlaceholder)
        {
            ClearCurrentNote();
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.NotePath))
        {
            currentNoteItemId = node.Id;
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(node.NotePath, SelectInTree: false));
            return;
        }
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        if (parameter is string notePath && !string.IsNullOrWhiteSpace(notePath))
        {
            OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(notePath, SelectInTree: true));
        }
    }

    private void ClearCurrentNoteState()
    {
        currentNotePath = null;
        currentNoteItemId = null;
        currentNoteTitle = string.Empty;
        lastSavedContent = string.Empty;
        ClearCurrentNoteToCardQueue();
        OnPropertyChanged(nameof(CurrentNotePath));
    }

    private void ClearCurrentNoteToCardQueue()
    {
    }
}
