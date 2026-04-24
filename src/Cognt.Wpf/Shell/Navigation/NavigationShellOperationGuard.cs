using CogntApp.Shell.Dialogs;

namespace CogntApp.Shell.Navigation;

internal static class NavigationShellOperationGuard
{
    internal static void RunWithWarning(
        Action action,
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
            action();
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
        }
    }

    internal static TResult RunWithWarning<TResult>(
        Func<TResult> action,
        IShellDialogService dialogs,
        string title,
        string failurePrefix,
        TResult fallbackValue)
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
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
            return fallbackValue;
        }
    }
}
