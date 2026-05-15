using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Editor;

internal sealed class StartupWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;

    internal StartupWorkflowController(
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
    }

    internal async Task InitializeAsync()
    {
        await EditorShellOperationGuard.RunWithWarningAsync(
            () =>
            {
                viewModel.Initialize();
                return Task.CompletedTask;
            },
            dialogs,
            "Startup Failed",
            "The collection could not be initialized");
    }
}
