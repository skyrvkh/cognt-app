using CogntApp.Shell.Dialogs;

namespace CogntApp.Shell.Editor;

internal static class EditorShellOperationGuard
{
    internal static async Task RunWithWarningAsync(
        Func<Task> action,
        IShellDialogService dialogs,
        string title,
        string failurePrefix)
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
            dialogs.ShowWarning(title, $"{failurePrefix}: {ex.Message}");
        }
    }
}
