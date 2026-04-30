using System.IO;
using CogntApp.Models.Markdown;
using CogntApp.Models.Navigation;
using CogntApp.Models.Tree;
using CogntApp.Services;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public string CreateExtractNote(string? sourcePath, string selectedContent, string selectedPlainText)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The extract source is not the current note file.");
        }

        long? parentId = currentNoteItemId;
        NoteTreeNodeViewModel? parentNode = parentId.HasValue
            ? FindTreeNode(parentId.Value)
            : null;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Extract";
        string content = string.IsNullOrWhiteSpace(selectedContent)
            ? BlockSuiteDocumentContent.FromPlainText(selectedPlainText, title)
            : selectedContent;
        CreatedNote note = noteService.CreateExtractNote(
            currentNotesDirectory,
            parentId,
            sourcePath,
            title,
            content);

        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return note.FileRelPath;
    }

    public LinkedNote CreateLinkedNote(string sourcePath, string viewMode)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The linked note source is not the current note file.");
        }

        NoteTreeNodeViewModel? parentNode = currentNoteItemId.HasValue
            ? FindTreeNode(currentNoteItemId.Value)
            : null;

        long? parentId = parentNode is { CanContainChildren: true }
            ? parentNode.Id
            : parentNode?.ParentId;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Untitled";
        string documentContent = BlockSuiteDocumentContent.FromPlainText(string.Empty, title);
        CreatedNote note = noteService.CreateNote(
            currentNotesDirectory,
            parentId,
            title,
            documentContent);

        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);

        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return new LinkedNote(note.NotePath, note.FileRelPath, title, documentContent);
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(currentNotesDirectory))
        {
            return;
        }

        CreatedNote note = noteService.CreateNote(
            currentNotesDirectory,
            parentId: null,
            "Untitled",
            BlockSuiteDocumentContent.FromPlainText(string.Empty, "Untitled"));
        TreeItemRow? item = noteService.FindNoteByPath(currentNotesDirectory, note.NotePath);
        AddLoadedTreeNode(item);
        AddOrUpdateRecentNote(item, note.NotePath);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteRequested?.Invoke(this, new OpenNoteRequestedEventArgs(note.NotePath, SelectInTree: true));
    }
}
