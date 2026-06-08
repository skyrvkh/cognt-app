namespace Cognt.Wpf.EditorHost.Views;

internal static class BlockSuiteBridgeDispatch
{
    public static bool TryHandleCurrentSourcePath(
        bool suppressBridgeCommands,
        Func<string?, bool> isCurrentSourcePath,
        string? path,
        Action onMismatch)
    {
        if (suppressBridgeCommands)
        {
            return false;
        }

        if (isCurrentSourcePath(path))
        {
            return true;
        }

        onMismatch();
        return false;
    }

    public static void TryInvokeHandler<TEventArgs>(
        object sender,
        EventHandler<TEventArgs>? handler,
        TEventArgs eventArgs,
        Action onMissing,
        Action<Exception> onFailure)
    {
        TryInvokeHandler(sender, handler, eventArgs, static () => { }, onMissing, onFailure);
    }

    public static void TryInvokeHandler<TEventArgs>(
        object sender,
        EventHandler<TEventArgs>? handler,
        TEventArgs eventArgs,
        Action beforeInvoke,
        Action onMissing,
        Action<Exception> onFailure)
    {
        if (handler is null)
        {
            onMissing();
            return;
        }

        try
        {
            beforeInvoke();
            handler.Invoke(sender, eventArgs);
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    public static async Task TryInvokeCurrentSourceAsyncHandler<TEventArgs, TResult>(
        object sender,
        Func<object?, TEventArgs, Task>? handler,
        TEventArgs eventArgs,
        string? sourcePath,
        bool suppressBridgeCommands,
        Func<string?, bool> isCurrentSourcePath,
        Action<TResult> complete,
        Func<string, TResult> fail,
        string sourceMismatchMessage,
        string missingHandlerMessage,
        Action<Exception> logFailure)
    {
        if (!TryHandleCurrentSourcePath(
                suppressBridgeCommands,
                isCurrentSourcePath,
                sourcePath,
                () => complete(fail(sourceMismatchMessage))))
        {
            return;
        }

        if (handler is null)
        {
            complete(fail(missingHandlerMessage));
            return;
        }

        try
        {
            await handler.Invoke(sender, eventArgs);
        }
        catch (Exception ex)
        {
            logFailure(ex);
            complete(fail(ex.Message));
        }
    }
}
