using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Cognt.Wpf.EditorHost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public void AttachBlockSuiteHostToEditor()
    {
        MountBlockSuiteHostInEditorFrame();
    }

    public async Task FocusEditorAsync()
    {
        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            return;
        }

        await FocusEditorHostAsync();
        await FocusEditorDocumentAsync();
    }

    public void DetachBlockSuiteHost()
    {
        SetBlockSuiteHostVisible(false);
        RemoveBlockSuiteHostFromParent();
    }

    public void DisposeEditorHost()
    {
        BeginEditorHostDisposal();
    }

    private void MountBlockSuiteHostInEditorFrame()
    {
        if (ReferenceEquals(EditorWebViewFrame.Child, BlockSuiteHostView))
        {
            return;
        }

        RemoveBlockSuiteHostFromParent();
        EditorWebViewFrame.Child = BlockSuiteHostView;
    }

    private void MountBlockSuiteHostInTransientHost(ITransientEditorHost host)
    {
        RemoveBlockSuiteHostFromParent();
        host.Mount(BlockSuiteHostView);
        host.PrepareForInput();
        PrepareBlockSuiteHostForInput();
    }

    private void RemoveBlockSuiteHostFromParent()
    {
        switch (BlockSuiteHostView.Parent)
        {
            case Border border when ReferenceEquals(border.Child, BlockSuiteHostView):
                border.Child = null;
                break;

            case ContentControl contentControl when ReferenceEquals(contentControl.Content, BlockSuiteHostView):
                contentControl.Content = null;
                break;

            case Panel panel:
                panel.Children.Remove(BlockSuiteHostView);
                break;
        }
    }

    private void PrepareBlockSuiteHostForInput()
    {
        BlockSuiteHostView.Visibility = Visibility.Visible;
        BlockSuiteHostView.IsEnabled = true;
        BlockSuiteHostView.Focusable = true;
    }

    private void SetBlockSuiteHostVisible(bool isVisible)
    {
        PrepareBlockSuiteHostForInput();
        BlockSuiteHostView.Opacity = isVisible ? 1 : 0;
        BlockSuiteHostView.IsHitTestVisible = isVisible;
    }

    private async Task FocusMountedBlockSuiteHostAsync(
        Action? updateHostLayout = null,
        Action? focusHost = null,
        Action<IInputElement>? setFocusedElement = null)
    {
        PrepareBlockSuiteHostForInput();
        updateHostLayout?.Invoke();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Input);
        Focus();
        focusHost?.Invoke();
        BlockSuiteHostView.Focus();
        Keyboard.Focus(BlockSuiteHostView);
        setFocusedElement?.Invoke(BlockSuiteHostView);
    }

    private async Task FocusEditorHostAsync()
    {
        SetBlockSuiteHostVisible(true);
        await FocusMountedBlockSuiteHostAsync();

        if (!IsHostReady)
        {
            await WarmUpAsync();
        }
    }

    private async Task FocusEditorDocumentAsync()
    {
        try
        {
            await blockSuiteBridge.FocusEditorAsync().WaitAsync(EditorFocusRequestTimeout);
        }
        catch (TimeoutException ex)
        {
            logger.LogDebug(ex, "EditorView focus request timed out.");
        }
    }

    private void BeginEditorHostDisposal()
    {
        if (isDisposingEditorHost)
        {
            return;
        }

        isDisposingEditorHost = true;
        suppressBridgeCommands = true;
        Loaded -= EditorView_Loaded;
        Unloaded -= EditorView_Unloaded;
        ClearTransientEditorState();
        RemoveBlockSuiteHostFromParent();

        _ = DisposeEditorHostAsync();
    }

    private async Task DisposeEditorHostAsync()
    {
        Task? disposeTask = TryStartEditorHostDisposal();
        if (disposeTask is null)
        {
            return;
        }

        if (!await TryAwaitEditorHostDisposalAsync(disposeTask))
        {
            return;
        }

        await CompleteEditorHostDisposalAsync(disposeTask);
    }

    private Task? TryStartEditorHostDisposal()
    {
        try
        {
            return BlockSuiteHostView.DisposeAsync().AsTask();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EditorView DisposeEditorHost could not start.");
            return null;
        }
    }

    private async Task<bool> TryAwaitEditorHostDisposalAsync(Task disposeTask)
    {
        Task completedTask = await Task.WhenAny(disposeTask, Task.Delay(EditorHostDisposeTimeout));
        if (completedTask == disposeTask)
        {
            return true;
        }

        logger.LogWarning(
            "EditorView DisposeEditorHost timed out. Timeout={Timeout} HostState={HostState} BridgeReady={BridgeReady}",
            EditorHostDisposeTimeout,
            hostState,
            blockSuiteBridge.IsReady);
        _ = ObserveLateEditorHostDisposalAsync(disposeTask);
        return false;
    }

    private async Task CompleteEditorHostDisposalAsync(Task disposeTask)
    {
        try
        {
            await disposeTask;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EditorView DisposeEditorHost failed.");
        }
    }

    private async Task ObserveLateEditorHostDisposalAsync(Task disposeTask)
    {
        try
        {
            await disposeTask;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "EditorView DisposeEditorHost completed with an exception after timeout.");
        }
    }
}
