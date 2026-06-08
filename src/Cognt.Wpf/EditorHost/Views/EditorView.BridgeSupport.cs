using System.IO;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private async Task FlushMentionTargetsAsync()
    {
        if (mentionTargets.Count == 0 || suppressBridgeCommands || !blockSuiteBridge.HasActiveHost)
        {
            return;
        }

        try
        {
            await blockSuiteBridge.SetMentionTargetsAsync(mentionTargets);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BlockSuite mention target update failed.");
        }
    }

    private Task DispatchCurrentSourceAsyncBridgeRequest<TEventArgs, TResult>(
        Func<object?, TEventArgs, Task>? handler,
        TEventArgs eventArgs,
        string? sourcePath,
        Action<TResult> complete,
        Func<string, TResult> fail,
        string sourceMismatchMessage,
        string missingHandlerMessage,
        Action<Exception> logFailure)
    {
        return BlockSuiteBridgeDispatch.TryInvokeCurrentSourceAsyncHandler(
            this,
            handler,
            eventArgs,
            sourcePath,
            suppressBridgeCommands,
            IsCurrentSourcePath,
            complete,
            fail,
            sourceMismatchMessage,
            missingHandlerMessage,
            logFailure);
    }

    private bool IsCurrentSourcePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(currentSourcePath)
            && string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentSourcePath), StringComparison.OrdinalIgnoreCase);
    }
}
