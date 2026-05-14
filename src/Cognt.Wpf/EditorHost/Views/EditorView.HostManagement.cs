using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Cognt.Wpf.EditorHost.Abstractions;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public async Task EditTransientNoteInHostAsync(
        ITransientEditorHost host,
        string title,
        string content,
        Action<string> contentChanged)
    {
        int requestId = ++loadRequestId;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            return;
        }

        SetBlockSuiteHostVisible(false);
        AttachBlockSuiteHostTo(host);
        PrepareInteractiveBlockSuiteHost(host);
        transientContentChanged = contentChanged;

        try
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.ActivateEditorAsync(),
                EditorOperationTimeout,
                "The shared BlockSuite editor did not leave read-only mode.");

            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadNoteAsync("card-workbench.json", title, content),
                GetDocumentLoadTimeout(content),
                "The shared BlockSuite editor did not finish loading the card text.");

            if (requestId != loadRequestId)
            {
                ClearTransientEditorState();
                return;
            }

            SetBlockSuiteHostVisible(true);
            await FocusBlockSuiteHostAsync(host);

            await WaitWithTimeoutAsync(
                blockSuiteBridge.FocusEditorAsync(),
                EditorOperationTimeout,
                "The shared BlockSuite editor did not accept focus.");

            if (requestId == loadRequestId)
            {
                isShowingTransientEditor = true;
            }
        }
        catch
        {
            if (requestId == loadRequestId)
            {
                ClearTransientEditorState();
                AttachBlockSuiteHostToEditor();
            }

            throw;
        }
        finally
        {
            if (requestId == loadRequestId)
            {
                SetBlockSuiteHostVisible(true);
            }
        }
    }

    public async Task RestoreNoteAfterTransientEditorAsync()
    {
        bool shouldRestore = isShowingTransientEditor;
        if (!shouldRestore)
        {
            return;
        }

        if (shouldRestore)
        {
            SetBlockSuiteHostVisible(false);
        }

        ClearTransientEditorState();
        AttachBlockSuiteHostToEditor();

        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            ShowNoNoteState();
            return;
        }

        try
        {
            ShowLoadingState("Loading editor", "Restoring the current note...");
            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadNoteAsync(currentSourcePath, currentTitle, currentContent, currentCollectionRoot),
                GetDocumentLoadTimeout(currentContent),
                "The editor did not restore the current note after card editing.");
            await FlushMentionTargetsAsync();
            ShowReadyState();
        }
        finally
        {
            SetBlockSuiteHostVisible(true);
        }
    }

    private void AttachBlockSuiteHostTo(ITransientEditorHost host)
    {
        RemoveBlockSuiteHostFromParent();
        host.Mount(BlockSuiteHostView);
        PrepareInteractiveBlockSuiteHost(host);
    }

    public void AttachBlockSuiteHostToEditor()
    {
        if (ReferenceEquals(EditorWebViewFrame.Child, BlockSuiteHostView))
        {
            return;
        }

        RemoveBlockSuiteHostFromParent();
        EditorWebViewFrame.Child = BlockSuiteHostView;
    }

    public void DetachBlockSuiteHost()
    {
        SetBlockSuiteHostVisible(false);
        RemoveBlockSuiteHostFromParent();
    }

    public void DisposeEditorHost()
    {
        suppressBridgeCommands = true;
        Loaded -= EditorView_Loaded;
        Unloaded -= EditorView_Unloaded;
        ClearTransientEditorState();
        RemoveBlockSuiteHostFromParent();

        try
        {
            if (BlockSuiteHostView is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            System.Diagnostics.Debug.WriteLine($"Editor host disposal skipped: {ex.Message}");
        }
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

    private void SetBlockSuiteHostVisible(bool isVisible)
    {
        BlockSuiteHostView.Visibility = Visibility.Visible;
        BlockSuiteHostView.IsEnabled = true;
        BlockSuiteHostView.Focusable = true;
        BlockSuiteHostView.Opacity = isVisible ? 1 : 0;
        BlockSuiteHostView.IsHitTestVisible = isVisible;
    }

    private void ClearTransientEditorState()
    {
        transientContentChanged = null;
        isShowingTransientEditor = false;
    }

    private void PrepareInteractiveBlockSuiteHost(ITransientEditorHost host)
    {
        host.PrepareForInput();
        BlockSuiteHostView.Visibility = Visibility.Visible;
        BlockSuiteHostView.IsEnabled = true;
        BlockSuiteHostView.Focusable = true;
    }

    private async Task FocusBlockSuiteHostAsync(ITransientEditorHost host)
    {
        PrepareInteractiveBlockSuiteHost(host);
        host.UpdateHostLayout();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Input);
        host.FocusHost();
        BlockSuiteHostView.Focus();
        Keyboard.Focus(BlockSuiteHostView);
        host.SetFocusedElement(BlockSuiteHostView);
    }
}
