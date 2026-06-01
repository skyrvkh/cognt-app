using System.Windows;
using Cognt.Wpf.EditorHost.Views;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Workspace;

internal static class WorkspaceEditorViewLoadAwaiter
{
    internal static async Task WaitAsync(
        EditorView editorView,
        TimeSpan timeout,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(editorView);

        if (editorView.IsLoaded)
        {
            return;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            editorView.Loaded -= OnLoaded;
            completion.TrySetResult();
        }

        editorView.Loaded += OnLoaded;
        try
        {
            Task completedTask = await Task.WhenAny(completion.Task, Task.Delay(timeout));
            if (!ReferenceEquals(completedTask, completion.Task))
            {
                logger?.LogError("Editor view load timed out. Timeout={Timeout}", timeout);
                throw new TimeoutException("The editor view did not finish loading.");
            }

            await completion.Task;
        }
        finally
        {
            editorView.Loaded -= OnLoaded;
        }
    }
}
