using CogntApp.Models.Markdown;
using CogntApp.Services;

namespace Cognt.Wpf.EditorHost.Services;

public sealed partial class BlockSuiteHostBridge
{
    private const string DiagnosticMessageType = "diagnostic";

    private readonly TaskCompletionSource readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IBlockSuiteHostClient? host;

    internal void Register(IBlockSuiteHostClient blockSuiteHost)
    {
        host = blockSuiteHost;
        readyCompletion.TrySetResult();
    }

    internal void RegisterStartupFailure(Exception exception)
    {
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

    public Task WaitUntilReadyAsync()
    {
        return readyCompletion.Task;
    }

    public async Task LoadNoteAsync(string? path, string title, string content, string? collectionRoot = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadNoteAsync(path, title, content, collectionRoot);
    }

    public async Task LoadCardAsync(string content)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadCardAsync(content);
    }

    public async Task BeginCardRenderAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.BeginCardRenderAsync();
    }

    public async Task ActivateEditorAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ActivateEditorAsync();
    }

    public async Task FocusEditorAsync()
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.FocusEditorAsync();
    }

    public async Task ShowSaveResultAsync(bool ok, string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowSaveResultAsync(ok, error);
    }

    public async Task ShowExtractResultAsync(bool ok, string? createdPath = null, string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowExtractResultAsync(ok, createdPath, error);
    }

    public async Task SetMentionTargetsAsync(IEnumerable<MentionTarget> targets)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.SetMentionTargetsAsync(targets);
    }

    public async Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? title = null,
        string? documentContent = null,
        string? error = null)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.HandleMentionPreviewResultAsync(
            requestId,
            relativePath,
            mode,
            ok,
            title,
            documentContent,
            error);
    }

    public async Task HandleMentionOpenResultAsync(bool ok, string? error = null)
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

