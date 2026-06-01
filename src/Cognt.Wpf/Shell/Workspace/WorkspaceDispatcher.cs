using System.Windows;

namespace CogntApp.Shell.Workspace;

internal static class WorkspaceDispatcher
{
    internal static Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current?.Dispatcher is not { } dispatcher
            || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }
}
