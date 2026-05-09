using System.IO;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private async Task FlushMentionTargetsAsync()
    {
        if (mentionTargets.Count == 0)
        {
            return;
        }

        try
        {
            await blockSuiteBridge.SetMentionTargetsAsync(mentionTargets);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite mention target update failed: {ex}");
        }
    }

    private bool TryHandleCurrentSourcePath(string? path, Action onMismatch)
    {
        if (IsCurrentSourcePath(path))
        {
            return true;
        }

        onMismatch();
        return false;
    }

    private void TryInvokeHandler<TEventArgs>(
        EventHandler<TEventArgs>? handler,
        TEventArgs eventArgs,
        Action onMissing,
        Action<Exception> onFailure)
    {
        TryInvokeHandler(handler, eventArgs, static () => { }, onMissing, onFailure);
    }

    private void TryInvokeHandler<TEventArgs>(
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
            handler.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    private bool IsCurrentSourcePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(currentSourcePath)
            && string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentSourcePath), StringComparison.OrdinalIgnoreCase);
    }
}
