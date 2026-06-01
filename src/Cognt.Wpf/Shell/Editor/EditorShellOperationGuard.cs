using CogntApp.Shell.Dialogs;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal static class EditorShellOperationGuard
{
    internal static async Task RunWithWarningAsync(
        Func<Task> action,
        IShellDialogService dialogs,
        string title,
        string failurePrefix,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(failurePrefix);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Guarded editor shell operation failed. Title={Title} FailurePrefix={FailurePrefix}", title, failurePrefix);
            dialogs.ShowWarning(title, $"{failurePrefix}: {ex.Message}");
        }
    }
}
