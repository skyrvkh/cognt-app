using System.Text.Json;
using CogntApp.Components;
using CogntApp.Models.Editor;
using CogntApp.Models.Markdown;

namespace CogntApp.Services;

public sealed class BlockSuiteHostBridge
{
    private const string DiagnosticMessageType = "diagnostic";

    private readonly TaskCompletionSource readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private BlockSuiteHost? host;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<string>? DiagnosticReceived;

    internal void Register(BlockSuiteHost blockSuiteHost)
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

    internal void Unregister(BlockSuiteHost blockSuiteHost)
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

    public async Task LoadNoteAsync(string? path, string title, string content)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadNoteAsync(path, title, content);
    }

    public async Task LoadCardAsync(string markdown)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadCardAsync(markdown);
    }

    public async Task BeginCardRenderAsync()
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.BeginCardRenderAsync();
    }

    public async Task ActivateEditorAsync()
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ActivateEditorAsync();
    }

    public async Task FocusEditorAsync()
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.FocusEditorAsync();
    }

    public async Task ShowSaveResultAsync(bool ok, string? error = null)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowSaveResultAsync(ok, error);
    }

    public async Task ShowExtractResultAsync(bool ok, string? createdPath = null, string? error = null)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.ShowExtractResultAsync(ok, createdPath, error);
    }

    public async Task SetMentionTargetsAsync(IEnumerable<MarkdownMentionTarget> targets)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.SetMentionTargetsAsync(targets);
    }

    public async Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? title = null,
        string? markdownExcerpt = null,
        string? error = null)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.HandleMentionPreviewResultAsync(
            requestId,
            relativePath,
            mode,
            ok,
            title,
            markdownExcerpt,
            error);
    }

    public async Task HandleMentionOpenResultAsync(bool ok, string? error = null)
    {
        BlockSuiteHost blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.HandleMentionOpenResultAsync(ok, error);
    }

    internal void RaiseContentChanged(string content)
    {
        ContentChanged?.Invoke(this, content);
    }

    internal void RaiseCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        CreateExtractRequested?.Invoke(
            this,
            new CreateExtractRequestedEventArgs(sourcePath, selectedContent, selectedPlainText));
    }

    internal void RaiseSaveCloze(string sourcePath, string selectedPlainText, string clozeText, string content)
    {
        SaveClozeRequested?.Invoke(
            this,
            new SaveClozeRequestedEventArgs(sourcePath, selectedPlainText, clozeText, content));
    }

    internal void RaiseMentionPreview(
        string requestId,
        string sourcePath,
        string relativePath,
        string mode,
        int maxCharacters)
    {
        MentionPreviewRequested?.Invoke(
            this,
            new MentionPreviewRequestedEventArgs(requestId, sourcePath, relativePath, mode, maxCharacters));
    }

    internal void RaiseOpenMention(string sourcePath, string relativePath, string content)
    {
        OpenMentionRequested?.Invoke(this, new OpenMentionRequestedEventArgs(sourcePath, relativePath, content));
    }

    internal void RaiseHostMessage(JsonElement message)
    {
        if (!message.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.GetString() != DiagnosticMessageType
            || !message.TryGetProperty("message", out JsonElement messageElement))
        {
            return;
        }

        DiagnosticReceived?.Invoke(this, messageElement.GetString() ?? string.Empty);
    }

    private async Task<BlockSuiteHost> WaitForHostAsync()
    {
        await WaitUntilReadyAsync();
        return host ?? throw new InvalidOperationException("The BlockSuite host is not ready.");
    }
}

public sealed record SaveClozeRequestedEventArgs(
    string SourcePath,
    string SelectedPlainText,
    string ClozeText,
    string Content);
