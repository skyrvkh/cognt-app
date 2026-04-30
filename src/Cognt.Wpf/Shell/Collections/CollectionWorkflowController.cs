using System.Windows.Controls.Primitives;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.Shell.Collections;

internal sealed class CollectionWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;

    internal CollectionWorkflowController(
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
    }

    internal void OpenSelector(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        viewModel.RefreshCollectionSummaries();
        popup.IsOpen = true;
    }

    internal void SelectCollection(CollectionOptionViewModel? collection, Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        if (collection is not null && viewModel.SelectCollectionCommand.CanExecute(collection))
        {
            viewModel.SelectCollectionCommand.Execute(collection);
        }

        popup.IsOpen = false;
    }

    internal void CreateCollection(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        CollectionCreationPromptResult? prompt = dialogs.PromptForCollectionCreation(
            "Create Collection",
            "New Collection",
            viewModel.GetDefaultCollectionCreationParentPath(),
            "Create");
        if (prompt is null)
        {
            return;
        }

        CollectionCreationRequest request = new(prompt.Name, prompt.ParentPath);
        if (viewModel.AddCollectionCommand.CanExecute(request))
        {
            viewModel.AddCollectionCommand.Execute(request);
        }

        popup.IsOpen = false;
    }

    internal void RemoveCurrentCollection(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        CollectionRemovalPlan plan = viewModel.GetCurrentCollectionRemovalPlan();
        if (!plan.CanRemove)
        {
            dialogs.ShowInfo("Remove Collection", plan.Message);
            return;
        }

        if (!dialogs.ConfirmWarning("Remove Collection", plan.Message))
        {
            return;
        }

        if (viewModel.RemoveCollectionCommand.CanExecute(null))
        {
            viewModel.RemoveCollectionCommand.Execute(null);
        }

        popup.IsOpen = false;
    }
}
