using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class StartupWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<StartupWorkflowController>? logger;

    internal StartupWorkflowController(
        MainViewModel viewModel,
        IShellDialogService dialogs,
        ILogger<StartupWorkflowController>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
        this.logger = logger;
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
            "The collection could not be initialized",
            logger);
    }
}
