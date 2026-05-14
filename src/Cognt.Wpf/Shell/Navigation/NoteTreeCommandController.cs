using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Navigation;

internal sealed class NoteTreeCommandController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;

    internal NoteTreeCommandController(
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
    }

    internal void CreateFolder(NoteTreeNodeViewModel? node, Action<string> beginInlineRenameById)
    {
        ArgumentNullException.ThrowIfNull(beginInlineRenameById);

        if (!viewModel.CanCreateFolderInNode(node))
        {
            dialogs.ShowInfo("New Folder", "A folder cannot be created here.");
            return;
        }

        NavigationShellOperationGuard.RunWithWarning(
            () =>
            {
                if (node is { CanContainChildren: true })
                {
                    node.IsExpanded = true;
                    viewModel.LoadChildren(node);
                }

                string createdId = viewModel.CreateFolder(node, "New Folder");
                beginInlineRenameById(createdId);
            },
            dialogs,
            "New Folder Failed",
            $"The folder could not be created:{Environment.NewLine}");
    }

    internal void BeginInlineRename(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (!viewModel.CanRenameNode(node))
        {
            dialogs.ShowInfo("Rename", "This item cannot be renamed.");
            return;
        }

        node.BeginRename();
    }

    internal void CommitInlineRename(
        NoteTreeNodeViewModel? node,
        Action<NoteTreeNodeViewModel> resumeRename)
    {
        ArgumentNullException.ThrowIfNull(resumeRename);

        if (node is null || !node.IsRenaming)
        {
            return;
        }

        string itemKind = viewModel.GetNodeKind(node);
        string newName = node.EditingTitle.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            dialogs.ShowWarning("Rename", "Name is required.");
            resumeRename(node);
            return;
        }

        if (string.Equals(newName, viewModel.GetEditableName(node), StringComparison.Ordinal))
        {
            node.CancelRename();
            return;
        }

        bool renameSucceeded = NavigationShellOperationGuard.RunWithWarning(
            () =>
            {
                viewModel.RenameNode(node, newName);
                node.EndRename();
                return true;
            },
            dialogs,
            "Rename Failed",
            $"The {itemKind} could not be renamed:{Environment.NewLine}",
            fallbackValue: false);

        if (!renameSucceeded)
        {
            resumeRename(node);
        }
    }

    internal void DeleteNode(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (!viewModel.CanDeleteNode(node))
        {
            dialogs.ShowInfo("Delete", "This item cannot be deleted.");
            return;
        }

        string itemKind = viewModel.GetNodeKind(node);
        string itemName = viewModel.GetEditableName(node);
        string message = itemKind == "note" && node.ChildCount == 0
            ? $"Delete note \"{itemName}\"?"
            : itemKind == "note"
                ? $"Delete note \"{itemName}\" and everything inside it?"
                : itemKind == "PDF"
                    ? $"Remove PDF \"{itemName}\" from the tree?"
                    : $"Delete folder \"{itemName}\" and everything inside it?";

        if (!dialogs.ConfirmWarning("Confirm Delete", message))
        {
            return;
        }

        NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.DeleteNode(node),
            dialogs,
            "Delete Failed",
            $"The {itemKind} could not be deleted:{Environment.NewLine}");
    }

    internal void DuplicateNode(NoteTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (!viewModel.CanDuplicateNode(node))
        {
            dialogs.ShowInfo("Duplicate", "Only notes can be duplicated.");
            return;
        }

        NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.DuplicateNote(node),
            dialogs,
            "Duplicate Failed",
            $"The note could not be duplicated:{Environment.NewLine}");
    }
}
