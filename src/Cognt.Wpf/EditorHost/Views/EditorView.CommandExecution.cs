using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private static readonly TimeSpan EditorOperationTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan EditorFocusRequestTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EditorHostDisposeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HostStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinimumDocumentLoadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumDocumentLoadTimeout = TimeSpan.FromMinutes(4);

    private static TimeSpan GetDocumentLoadTimeout(string? content)
    {
        int contentLength = content?.Length ?? 0;
        int totalSeconds = 30 + (int)Math.Ceiling(contentLength / 10_000d);

        return TimeSpan.FromSeconds(Math.Clamp(
            totalSeconds,
            (int)MinimumDocumentLoadTimeout.TotalSeconds,
            (int)MaximumDocumentLoadTimeout.TotalSeconds));
    }

    private async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, string timeoutMessage)
    {
        await WaitForTaskCompletionAsync(task, timeout, timeoutMessage);
        await task;
    }

    private async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
    {
        await WaitForTaskCompletionAsync(task, timeout, timeoutMessage);
        return await task;
    }

    private async Task WaitForTaskCompletionAsync(Task task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask == task)
        {
            return;
        }

        logger.LogError(
            "Editor operation timed out. Operation={Operation} Timeout={Timeout}",
            timeoutMessage,
            timeout);
        RunBlockSuiteCommand(task, "timed-out operation");
        throw new TimeoutException(timeoutMessage);
    }

    private void RunBlockSuiteCommand(Task task, string operation)
    {
        if (suppressBridgeCommands)
        {
            return;
        }

        _ = ObserveBlockSuiteCommandAsync(task, operation);
    }

    private async Task ObserveBlockSuiteCommandAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            if (isDisposingEditorHost || suppressBridgeCommands)
            {
                logger.LogDebug(ex, "BlockSuite command stopped during host disposal. Operation={Operation}", operation);
                return;
            }

            logger.LogError(ex, "BlockSuite command failed. Operation={Operation}", operation);
        }
    }
}
