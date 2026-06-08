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
        int requestId = BeginTransientLoadRequest();

        await WarmUpAsync();

        if (!IsCurrentLoadRequest(requestId))
        {
            return;
        }

        BeginTransientEditorSession(host, contentChanged);

        try
        {
            await LoadTransientEditorContentAsync(title, content);
            bool activated = await TryCompleteTransientEditorSessionAsync(host, requestId);
            if (!activated)
            {
                return;
            }
        }
        catch
        {
            RestoreEditorHostAfterTransientFailure(requestId);
            throw;
        }
        finally
        {
            if (IsCurrentLoadRequest(requestId))
            {
                SetBlockSuiteHostVisible(true);
            }
        }
    }

    public async Task RestoreNoteAfterTransientEditorAsync()
    {
        if (!IsShowingTransientEditor)
        {
            return;
        }

        await RestorePrimaryEditorHostAfterTransientSessionAsync();
    }

    private int BeginTransientLoadRequest()
    {
        ClearTransientEditorState();
        return ++loadRequestId;
    }

    private void BeginTransientEditorSession(
        ITransientEditorHost host,
        Action<string> contentChanged)
    {
        SetBlockSuiteHostVisible(false);
        MountBlockSuiteHostInTransientHost(host);
        transientContentChanged = contentChanged;
    }

    private async Task LoadTransientEditorContentAsync(string title, string content)
    {
        await WaitWithTimeoutAsync(
            blockSuiteBridge.ActivateEditorAsync(),
            EditorOperationTimeout,
            "The shared BlockSuite editor did not leave read-only mode.");

        await LoadNoteIntoHostAsync(
            "card-workbench.json",
            title,
            content,
            collectionRoot: null,
            "The shared BlockSuite editor did not finish loading the card text.");
    }

    private async Task<bool> TryCompleteTransientEditorSessionAsync(
        ITransientEditorHost host,
        int requestId)
    {
        if (!IsCurrentLoadRequest(requestId))
        {
            ClearTransientEditorState();
            return false;
        }

        SetBlockSuiteHostVisible(true);
        await FocusMountedBlockSuiteHostAsync(
            host.UpdateHostLayout,
            host.FocusHost,
            host.SetFocusedElement);

        await WaitWithTimeoutAsync(
            blockSuiteBridge.FocusEditorAsync(),
            EditorOperationTimeout,
            "The shared BlockSuite editor did not accept focus.");

        if (IsCurrentLoadRequest(requestId))
        {
            isShowingTransientEditor = true;
        }

        return IsCurrentLoadRequest(requestId);
    }

    private void RestoreEditorHostAfterTransientFailure(int requestId)
    {
        if (!IsCurrentLoadRequest(requestId))
        {
            return;
        }

        ClearTransientEditorState();
        AttachBlockSuiteHostToEditor();
    }

    private async Task RestorePrimaryEditorHostAfterTransientSessionAsync()
    {
        PreparePrimaryEditorHostAfterTransientSession();

        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            ShowNoNoteState();
            return;
        }

        try
        {
            ShowLoadingState("Loading editor", "Restoring the current note...");
            await RestoreCurrentNoteIntoPrimaryEditorHostAsync();
            ShowReadyState();
        }
        finally
        {
            SetBlockSuiteHostVisible(true);
        }
    }

    private void PreparePrimaryEditorHostAfterTransientSession()
    {
        SetBlockSuiteHostVisible(false);
        ClearTransientEditorState();
        AttachBlockSuiteHostToEditor();
    }

    private async Task RestoreCurrentNoteIntoPrimaryEditorHostAsync()
    {
        await ReloadCurrentNoteIntoHostAsync("The editor did not restore the current note after card editing.");
        await FlushMentionTargetsAsync();
    }

    private void ClearTransientEditorState()
    {
        transientContentChanged = null;
        isShowingTransientEditor = false;
    }
}
