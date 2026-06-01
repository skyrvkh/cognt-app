namespace CogntApp.Shell.Editor;

internal static class EditorHostRequestSupport
{
    internal static void Invoke<TResult>(
        Func<TResult> action,
        Action<TResult> onSuccess,
        Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            onSuccess(action());
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    internal static async Task InvokeAsync<TResult>(
        Func<Task<TResult>> action,
        Action<TResult> onSuccess,
        Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            onSuccess(await action());
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    internal static async Task InvokeAsync(
        Func<Task> action,
        Action onSuccess,
        Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            await action();
            onSuccess();
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }
}
