using System.Windows;
using System.Windows.Threading;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public async Task WarmUpAsync()
    {
        await EnsureViewLoadedAsync();
        UpdateLayout();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        await WaitWithTimeoutAsync(
            blockSuiteBridge.WaitUntilReadyAsync(),
            HostStartupTimeout,
            "The BlockSuite host did not finish starting.");
        hostState = EditorHostState.Ready;

        if (currentTheme is not null)
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.ApplyThemeAsync(currentTheme),
                EditorOperationTimeout,
                "The editor did not apply the active theme.");
        }
    }

    public bool IsHostReady => hostState == EditorHostState.Ready && blockSuiteBridge.IsReady;

    public bool NeedsHostRestore => needsHostRestore;

    private async Task EnsureViewLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        TaskCompletionSource loadedCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            loadedCompletion.TrySetResult();
        }

        Loaded += OnLoaded;

        try
        {
            if (IsLoaded)
            {
                return;
            }

            await WaitWithTimeoutAsync(
                loadedCompletion.Task,
                HostStartupTimeout,
                "The editor view did not finish loading.");
        }
        finally
        {
            Loaded -= OnLoaded;
        }
    }
}
