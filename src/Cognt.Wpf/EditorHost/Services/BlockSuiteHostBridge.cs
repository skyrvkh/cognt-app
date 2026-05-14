using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace Cognt.Wpf.EditorHost.Services;

public sealed partial class BlockSuiteHostBridge
{
    private const string DiagnosticMessageType = "diagnostic";

    private TaskCompletionSource readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IBlockSuiteHostClient? host;
    private bool startupFailed;

    internal BlockSuiteHostBridge()
    {
    }

    internal void Register(IBlockSuiteHostClient blockSuiteHost)
    {
        host = blockSuiteHost;
        startupFailed = false;
        readyCompletion.TrySetResult();
    }

    internal void RegisterStartupFailure(Exception exception)
    {
        startupFailed = true;
        readyCompletion.TrySetException(new InvalidOperationException(
            "The BlockSuite host failed to start.",
            exception));
        DiagnosticReceived?.Invoke(this, exception.ToString());
    }

    internal void Unregister(IBlockSuiteHostClient blockSuiteHost)
    {
        if (ReferenceEquals(host, blockSuiteHost))
        {
            host = null;
        }
    }

    internal bool IsReady => readyCompletion.Task.IsCompletedSuccessfully && host is not null;

    internal bool HasActiveHost => host is not null;

    internal bool HasStartupFailure => startupFailed;

    internal void ResetForHostReload()
    {
        host = null;
        startupFailed = false;
        readyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal Task WaitUntilReadyAsync()
    {
        return readyCompletion.Task;
    }

    internal async Task LoadNoteAsync(string? path, string title, string content, string? collectionRoot = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadNoteAsync(path, title, content, collectionRoot);
    }

    internal async Task ActivateEditorAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ActivateEditorAsync();
    }

    internal async Task SetReadOnlyModeAsync(bool isReadOnly)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.SetReadOnlyModeAsync(isReadOnly);
    }

    internal async Task FocusEditorAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.FocusEditorAsync();
    }

    internal async Task CreateClozeFromSelectionAsync(
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.CreateClozeFromSelectionAsync(cardContextId, slotId, displayClozeIndex);
    }

    internal async Task<SelectedImageBlockReadModel?> GetSelectedImageBlockAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        return await blockSuiteHost.GetSelectedImageBlockAsync();
    }

    internal async Task<SelectedCardContextBlocksReadModel?> GetSelectedCardContextBlocksAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        return await blockSuiteHost.GetSelectedCardContextBlocksAsync();
    }

    internal async Task ShowSaveResultAsync(bool ok, string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowSaveResultAsync(ok, error);
    }

    internal async Task ShowExtractResultAsync(bool ok, string? createdPath = null, string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowExtractResultAsync(ok, createdPath, error);
    }

    internal async Task SetMentionTargetsAsync(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.SetMentionTargetsAsync(targets);
    }

    internal async Task SetDocumentLinksAsync(KnowledgeDocumentLinksReadModel links)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.SetDocumentLinksAsync(links);
    }

    internal async Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent = null,
        string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.HandleMentionPreviewResultAsync(
            requestId,
            relativePath,
            mode,
            ok,
            documentContent,
            error);
    }

    internal async Task HandleMentionOpenResultAsync(bool ok, string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.HandleMentionOpenResultAsync(ok, error);
    }

    private async Task<IBlockSuiteHostClient> WaitForHostAsync()
    {
        await WaitUntilReadyAsync();
        return host ?? throw new InvalidOperationException("The BlockSuite host is not ready.");
    }
}
