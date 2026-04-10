using System.IO;
using System.Text.Json;
using CogntApp.Models.Markdown;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CreateExtractRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateExtractRequestedEventArgs;
using CreateLinkedNoteRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteRequestedEventArgs;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;
using MentionPreviewRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionPreviewRequestedEventArgs;
using MentionSearchRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionSearchRequestedEventArgs;
using OpenMentionRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.OpenMentionRequestedEventArgs;
using SaveClozeRequestedEventArgs = CogntApp.Models.Editor.SaveClozeRequestedEventArgs;

namespace CogntApp.Services;

public sealed class BlockSuiteHostBridge
{
    private const string DiagnosticMessageType = "diagnostic";

    private readonly TaskCompletionSource readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IBlockSuiteHostClient? host;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    public event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    public event EventHandler<string>? DiagnosticReceived;

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

    public async Task LoadNoteAsync(string? path, string title, string content)
    {
        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await blockSuiteHost.LoadNoteAsync(path, title, content);
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

    internal IReadOnlyList<MentionTarget> RaiseMentionSearch(string sourcePath, string query, int limit)
    {
        MentionSearchRequestedEventArgs args = new(sourcePath, query, limit);
        MentionSearchRequested?.Invoke(this, args);
        return args.Targets.Select(ToMentionTarget).ToArray();
    }

    internal void RaiseOpenMention(string sourcePath, string relativePath, string content)
    {
        OpenMentionRequested?.Invoke(this, new OpenMentionRequestedEventArgs(sourcePath, relativePath, content));
    }

    internal CreateLinkedNoteResult RaiseCreateLinkedNote(string sourcePath, string viewMode)
    {
        CreateLinkedNoteRequestedEventArgs args = new(sourcePath, viewMode);
        CreateLinkedNoteRequested?.Invoke(this, args);
        return args.Result;
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

    private async Task<IBlockSuiteHostClient> WaitForHostAsync()
    {
        await WaitUntilReadyAsync();
        return host ?? throw new InvalidOperationException("The BlockSuite host is not ready.");
    }

    private static MentionTarget ToMentionTarget(KnowledgeMentionTargetReadModel target)
    {
        return new MentionTarget(
            target.RelativePath,
            target.Title,
            Path.GetFileName(target.RelativePath),
            0);
    }
}
