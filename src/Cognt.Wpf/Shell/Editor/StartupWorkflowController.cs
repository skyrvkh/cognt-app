using System.Windows.Threading;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Editor;

internal sealed class StartupWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly EditorWorkflowCoordinator editorWorkflow;
    private readonly IShellDialogService dialogs;

    internal StartupWorkflowController(
        MainViewModel viewModel,
        EditorWorkflowCoordinator editorWorkflow,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorWorkflow);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.editorWorkflow = editorWorkflow;
        this.dialogs = dialogs;
    }

    internal async Task InitializeAsync()
    {
        await EditorShellOperationGuard.RunWithWarningAsync(
            async () =>
            {
                editorWorkflow.AttachBlockSuiteHostToEditor();
                await Dispatcher.Yield(DispatcherPriority.Loaded);
                await editorWorkflow.WarmUpAsync();
            },
            dialogs,
            "Editor Startup",
            "The editor host could not finish starting");

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
