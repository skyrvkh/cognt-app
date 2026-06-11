using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Navigation;

internal sealed class BreadcrumbCommandController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<BreadcrumbCommandController> logger;

    internal BreadcrumbCommandController(
        MainViewModel viewModel,
        IShellDialogService dialogs,
        ILogger<BreadcrumbCommandController> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
        this.logger = logger;
    }

    internal bool NavigateTo(EditorBreadcrumbItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return NavigationShellOperationGuard.RunWithWarning(
            () => viewModel.NavigateToBreadcrumb(item),
            dialogs,
            "Open Breadcrumb",
            $"The breadcrumb item could not be opened:{Environment.NewLine}",
            fallbackValue: false);
    }

    internal void BeginRename(EditorBreadcrumbItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.BeginEdit();
    }

    internal void CommitRename(EditorBreadcrumbItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsEditing)
        {
            return;
        }

        string newName = item.EditingTitle.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            dialogs.ShowWarning("Rename", "Name is required.");
            item.BeginEdit();
            return;
        }

        if (string.Equals(newName, item.Title, StringComparison.Ordinal))
        {
            item.CancelEdit();
            return;
        }

        bool renameSucceeded = NavigationShellOperationGuard.RunWithWarning(
            () =>
            {
                viewModel.RenameBreadcrumbItem(item, newName);
                return true;
            },
            dialogs,
            "Rename Failed",
            $"The {item.KindLabel} could not be renamed:{Environment.NewLine}",
            fallbackValue: false);

        if (!renameSucceeded)
        {
            logger.LogWarning("Breadcrumb rename failed; returning to edit mode. ItemId={ItemId} NewName={NewName}", item.Id, newName);
            item.BeginEdit();
            return;
        }

    }
}
