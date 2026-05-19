using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Services;

public sealed partial class BlockSuiteHostBridge
{
    private const string DiagnosticMessageType = "diagnostic";

    private TaskCompletionSource readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger<BlockSuiteHostBridge>? logger;
    private IBlockSuiteHostClient? host;
    private bool startupFailed;

    internal BlockSuiteHostBridge(ILogger<BlockSuiteHostBridge>? logger = null)
    {
        this.logger = logger;
    }

    internal void Register(IBlockSuiteHostClient blockSuiteHost)
    {
        logger?.LogInformation("BlockSuite host registered. ClientType={ClientType}", blockSuiteHost.GetType().Name);
        host = blockSuiteHost;
        startupFailed = false;
        readyCompletion.TrySetResult();
    }

    internal void RegisterStartupFailure(Exception exception)
    {
        logger?.LogError(exception, "BlockSuite host startup failed.");
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
            logger?.LogInformation("BlockSuite host unregistered. ClientType={ClientType}", blockSuiteHost.GetType().Name);
            host = null;
        }
    }

    internal bool IsReady => readyCompletion.Task.IsCompletedSuccessfully && host is not null;

    internal bool HasActiveHost => host is not null;

    internal bool HasStartupFailure => startupFailed;

    internal void ResetForHostReload()
    {
        logger?.LogDebug("BlockSuite host bridge reset for host reload.");
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
        logger?.LogInformation(
            "BlockSuite bridge loading note. Path={Path} Title={Title} ContentLength={ContentLength} CollectionRoot={CollectionRoot}",
            path,
            title,
            content.Length,
            collectionRoot);
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadNoteAsync(path, title, content, collectionRoot);
        logger?.LogInformation("BlockSuite bridge loaded note. Path={Path}", path);
    }

    internal async Task<string> ExportContentAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        return await blockSuiteHost.ExportContentAsync();
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

    internal async Task ApplyThemeAsync(EditorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        logger?.LogDebug("BlockSuite bridge applying theme. Theme={ThemeName}", theme.Name);
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ApplyThemeAsync(theme);
    }

    internal async Task FocusEditorAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.FocusEditorAsync();
    }

    internal async Task FocusBlockAsync(string blockId)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.FocusBlockAsync(blockId);
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
        if (!readyCompletion.Task.IsCompleted)
        {
            logger?.LogDebug("Waiting for BlockSuite host readiness.");
        }

        await WaitUntilReadyAsync();
        if (host is null)
        {
            logger?.LogError("BlockSuite host readiness completed without an active host.");
            throw new InvalidOperationException("The BlockSuite host is not ready.");
        }

        return host;
    }
}
