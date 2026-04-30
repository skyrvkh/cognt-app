using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public bool CanRenameNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public bool CanDeleteNode(NoteTreeNodeViewModel? node)
    {
        return CanModifyNode(node);
    }

    public string GetEditableName(NoteTreeNodeViewModel node)
    {
        return node.Title;
    }

    public string GetNodeKind(NoteTreeNodeViewModel node)
    {
        return node.Kind switch
        {
            KnowledgeItemKind.Note => "note",
            KnowledgeItemKind.PdfDocument => "PDF",
            _ => "folder"
        };
    }

    public bool CanCreateFolderInNode(NoteTreeNodeViewModel? node)
    {
        return !string.IsNullOrWhiteSpace(openCollectionPath)
            && (node is null || node.CanContainChildren);
    }

    public bool CanMoveNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
    }

    private bool CanModifyNode(NoteTreeNodeViewModel? node)
    {
        return node is not null
            && !node.IsPlaceholder;
    }

    private static long? GetFolderCreationParentId(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.CanContainChildren)
        {
            return node.Id;
        }

        return node.ParentId;
    }

    private void EnsureCanModifyNode(NoteTreeNodeViewModel node, string operation)
    {
        if (!CanModifyNode(node))
        {
            throw new InvalidOperationException($"This item cannot be {operation}.");
        }
    }

    private void EnsureBreadcrumbCanBeRenamed(Navigation.EditorBreadcrumbItemViewModel item, string newName)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("No collection is open.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (string.Equals(newName, item.Title, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The name has not changed.");
        }
    }
}
