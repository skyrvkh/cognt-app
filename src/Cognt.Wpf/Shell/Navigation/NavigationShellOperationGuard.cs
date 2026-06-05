using CogntApp.Shell.Dialogs;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Navigation;

internal static class NavigationShellOperationGuard
{
    internal static void RunWithWarning(
        Action action,
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
            action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Guarded navigation operation failed. Title={Title} FailurePrefix={FailurePrefix}", title, failurePrefix);
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
        }
    }

    internal static async Task<bool> RunWithWarningAsync(
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
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Guarded async navigation operation failed. Title={Title} FailurePrefix={FailurePrefix}", title, failurePrefix);
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
            return false;
        }
    }

    internal static TResult RunWithWarning<TResult>(
        Func<TResult> action,
        IShellDialogService dialogs,
        string title,
        string failurePrefix,
        TResult fallbackValue,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(failurePrefix);

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Guarded navigation operation with result failed. Title={Title} FailurePrefix={FailurePrefix}", title, failurePrefix);
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
            return fallbackValue;
        }
    }
}
