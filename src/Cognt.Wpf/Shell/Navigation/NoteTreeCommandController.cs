using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CogntApp.Shell.Navigation;

internal sealed class NoteTreeCommandController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<NoteTreeCommandController> logger;

    internal NoteTreeCommandController(MainViewModel viewModel, IShellDialogService dialogs)
        : this(viewModel, dialogs, NullLogger<NoteTreeCommandController>.Instance)
    {
    }

    internal NoteTreeCommandController(
        MainViewModel viewModel,
        IShellDialogService dialogs,
        ILogger<NoteTreeCommandController> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
        this.logger = logger;
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

    internal void CreateNote(NoteTreeNodeViewModel? node, Action<string> beginInlineRenameById)
    {
        ArgumentNullException.ThrowIfNull(beginInlineRenameById);

        if (!viewModel.CanCreateNoteInNode(node))
        {
            dialogs.ShowInfo("New Note", "A note cannot be created here.");
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

                string createdId = viewModel.CreateNote(node, "Untitled");
                beginInlineRenameById(createdId);
            },
            dialogs,
            "New Note Failed",
            $"The note could not be created:{Environment.NewLine}");
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

    internal Task<bool> DeleteNodeAsync(NoteTreeNodeViewModel? node)
    {
        return node is null
            ? Task.FromResult(false)
            : DeleteNodesAsync([node]);
    }

    internal void DeleteNode(NoteTreeNodeViewModel? node)
    {
        DeleteNodeAsync(node).GetAwaiter().GetResult();
    }

    internal async Task<bool> DeleteNodesAsync(IReadOnlyList<NoteTreeNodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        NoteTreeNodeViewModel[] deletableNodes = nodes
            .Where(viewModel.CanDeleteNode)
            .GroupBy(node => node.Id)
            .Select(group => group.First())
            .ToArray();
        if (deletableNodes.Length == 0)
        {
            return false;
        }

        if (deletableNodes.Length == 1)
        {
            return await DeleteSingleNodeAsync(deletableNodes[0]);
        }

        string message = $"Delete {deletableNodes.Length} selected items and everything inside them?";
        if (!dialogs.ConfirmWarning("Confirm Delete", message))
        {
            return false;
        }

        return await NavigationShellOperationGuard.RunWithWarningAsync(
            async () =>
            {
                await viewModel.DeleteNodesAsync(deletableNodes);
            },
            dialogs,
            "Delete Failed",
            $"The selected items could not be deleted:{Environment.NewLine}");
    }

    private async Task<bool> DeleteSingleNodeAsync(NoteTreeNodeViewModel node)
    {
        if (!viewModel.CanDeleteNode(node))
        {
            dialogs.ShowInfo("Delete", "This item cannot be deleted.");
            return false;
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
            return false;
        }

        return await NavigationShellOperationGuard.RunWithWarningAsync(
            async () =>
            {
                try
                {
                    await viewModel.DeleteNodeAsync(node);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Navigation item delete failed. ItemId={ItemId} Kind={ItemKind} Name={ItemName}",
                        node.Id,
                        itemKind,
                        itemName);
                    throw;
                }
            },
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
