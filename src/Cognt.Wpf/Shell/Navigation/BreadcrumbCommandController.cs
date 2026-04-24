using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.Shell.Navigation;

internal sealed class BreadcrumbCommandController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;

    internal BreadcrumbCommandController(
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
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
            item.BeginEdit();
        }
    }
}
