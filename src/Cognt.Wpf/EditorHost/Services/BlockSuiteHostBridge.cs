using System.Text.Json;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Services;

public sealed class BlockSuiteHostBridge
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

    internal event EventHandler? DocumentChanged;
    internal event EventHandler<string>? ContentChanged;
    internal event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    internal event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    internal event EventHandler<OpenImageOcclusionRequestedEventArgs>? OpenImageOcclusionRequested;
    internal event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    internal event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    internal event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    internal event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    internal event Func<object?, AttachFileRequestedEventArgs, Task>? AttachFileRequested;
    internal event Func<object?, AttachImageSourceRequestedEventArgs, Task>? AttachImageSourceRequested;
    internal event Func<object?, ReadAttachmentRequestedEventArgs, Task>? ReadAttachmentRequested;
    internal event Func<object?, StoreEditorBlobRequestedEventArgs, Task>? StoreEditorBlobRequested;
    internal event Func<object?, ReadEditorBlobRequestedEventArgs, Task>? ReadEditorBlobRequested;
    internal event Func<object?, ListEditorBlobsRequestedEventArgs, Task>? ListEditorBlobsRequested;
    internal event EventHandler<EditorDiagnosticReceivedEventArgs>? DiagnosticReceived;

    internal void Register(IBlockSuiteHostClient blockSuiteHost)
    {
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
        DiagnosticReceived?.Invoke(
            this,
            new EditorDiagnosticReceivedEventArgs(EditorDiagnosticLevel.Error, exception.ToString()));
    }

    internal void Unregister(IBlockSuiteHostClient blockSuiteHost)
    {
        if (ReferenceEquals(host, blockSuiteHost))
        {
            host = null;
        }
    }

    internal void FailPendingStartup(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (readyCompletion.Task.IsCompleted)
        {
            return;
        }

        logger?.LogWarning(exception, "BlockSuite host startup was cancelled before readiness.");
        startupFailed = true;
        readyCompletion.TrySetException(exception);
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

    internal Task LoadNoteAsync(string? path, string title, string content, string? collectionRoot = null)
    {
        return InvokeHostAsync(host => host.LoadNoteAsync(path, title, content, collectionRoot));
    }

    internal Task<string> ExportContentAsync()
    {
        return InvokeHostAsync(host => host.ExportContentAsync());
    }

    internal Task ActivateEditorAsync()
    {
        return InvokeHostAsync(host => host.ActivateEditorAsync());
    }

    internal Task SetReadOnlyModeAsync(bool isReadOnly)
    {
        return InvokeHostAsync(host => host.SetReadOnlyModeAsync(isReadOnly));
    }

    internal Task ApplyThemeAsync(EditorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return InvokeHostAsync(host => host.ApplyThemeAsync(theme));
    }

    internal Task FocusEditorAsync()
    {
        return InvokeHostAsync(host => host.FocusEditorAsync());
    }

    internal Task FocusBlockAsync(string blockId)
    {
        return InvokeHostAsync(host => host.FocusBlockAsync(blockId));
    }

    internal Task<EditorCaretState?> GetCaretStateAsync()
    {
        return InvokeHostAsync(host => host.GetCaretStateAsync());
    }

    internal Task RestoreCaretStateAsync(EditorCaretState caret)
    {
        ArgumentNullException.ThrowIfNull(caret);
        return InvokeHostAsync(host => host.RestoreCaretStateAsync(caret));
    }

    internal Task CreateClozeFromSelectionAsync(
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        return InvokeHostAsync(host => host.CreateClozeFromSelectionAsync(cardContextId, slotId, displayClozeIndex));
    }

    internal Task<SelectedImageBlockReadModel?> GetSelectedImageBlockAsync()
    {
        return InvokeHostAsync(host => host.GetSelectedImageBlockAsync());
    }

    internal Task<SelectedCardContextBlocksReadModel?> GetSelectedCardContextBlocksAsync()
    {
        return InvokeHostAsync(host => host.GetSelectedCardContextBlocksAsync());
    }

    internal Task ShowSaveResultAsync(bool ok, string? error = null)
    {
        return InvokeHostAsync(host => host.ShowSaveResultAsync(ok, error));
    }

    internal Task ShowExtractResultAsync(bool ok, string? createdPath = null, string? error = null)
    {
        return InvokeHostAsync(host => host.ShowExtractResultAsync(ok, createdPath, error));
    }

    internal Task SetMentionTargetsAsync(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        return InvokeHostAsync(host => host.SetMentionTargetsAsync(targets));
    }

    internal Task SetDocumentLinksAsync(KnowledgeDocumentLinksReadModel links)
    {
        return InvokeHostAsync(host => host.SetDocumentLinksAsync(links));
    }

    internal Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent = null,
        string? error = null)
    {
        return InvokeHostAsync(host => host.HandleMentionPreviewResultAsync(
            requestId,
            relativePath,
            mode,
            ok,
            documentContent,
            error));
    }

    internal Task HandleMentionOpenResultAsync(bool ok, string? error = null)
    {
        return InvokeHostAsync(host => host.HandleMentionOpenResultAsync(ok, error));
    }

    internal void RaiseContentChanged(string content)
    {
        ContentChanged?.Invoke(this, content);
    }

    internal void RaiseDocumentChanged()
    {
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        CreateExtractRequested?.Invoke(
            this,
            new CreateExtractRequestedEventArgs(sourcePath, selectedContent, selectedPlainText));
    }

    internal void RaiseSaveCloze(
        string sourcePath,
        string blockId,
        string selectedPlainText,
        string clozeText,
        string content,
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        SaveClozeRequested?.Invoke(
            this,
            new SaveClozeRequestedEventArgs(
                sourcePath,
                blockId,
                selectedPlainText,
                clozeText,
                content,
                cardContextId,
                slotId,
                displayClozeIndex));
    }

    internal void RaiseOpenImageOcclusion(
        string sourcePath,
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        OpenImageOcclusionRequested?.Invoke(
            this,
            new OpenImageOcclusionRequestedEventArgs(sourcePath, imageBlockId, imageRelPath, content));
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

    internal IReadOnlyList<KnowledgeMentionTargetReadModel> RaiseMentionSearch(string sourcePath, string query, int limit)
    {
        MentionSearchRequestedEventArgs args = new(sourcePath, query, limit);
        MentionSearchRequested?.Invoke(this, args);
        return args.Targets;
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

    internal async Task<AttachFileResult> RaiseAttachFileAsync(
        string sourcePath,
        string fileName,
        string contentBase64)
    {
        AttachFileRequestedEventArgs args = new(sourcePath, fileName, contentBase64);
        await InvokeAsyncHandlers(AttachFileRequested, args);
        return args.Result;
    }

    internal async Task<ReadAttachmentResult> RaiseReadAttachmentAsync(
        string sourcePath,
        string imageSource)
    {
        ReadAttachmentRequestedEventArgs args = new(sourcePath, imageSource);
        await InvokeAsyncHandlers(ReadAttachmentRequested, args);
        return args.Result;
    }

    internal async Task<EditorBlobResult> RaiseStoreEditorBlobAsync(
        string sourcePath,
        string blobId,
        string contentType,
        string contentBase64)
    {
        StoreEditorBlobRequestedEventArgs args = new(sourcePath, blobId, contentType, contentBase64);
        await InvokeAsyncHandlers(StoreEditorBlobRequested, args);
        return args.Result;
    }

    internal async Task<EditorBlobReadResult> RaiseReadEditorBlobAsync(
        string sourcePath,
        string blobId)
    {
        ReadEditorBlobRequestedEventArgs args = new(sourcePath, blobId);
        await InvokeAsyncHandlers(ReadEditorBlobRequested, args);
        return args.Result;
    }

    internal async Task<EditorBlobListResult> RaiseListEditorBlobsAsync(string sourcePath)
    {
        ListEditorBlobsRequestedEventArgs args = new(sourcePath);
        await InvokeAsyncHandlers(ListEditorBlobsRequested, args);
        return args.Result;
    }

    internal async Task<AttachFileResult> RaiseAttachImageSourceAsync(
        string sourcePath,
        string imageSource,
        string fallbackFileName)
    {
        AttachImageSourceRequestedEventArgs args = new(sourcePath, imageSource, fallbackFileName);
        await InvokeAsyncHandlers(AttachImageSourceRequested, args);
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

        string diagnosticMessage = ReadDiagnosticMessage(messageElement);
        if (string.IsNullOrWhiteSpace(diagnosticMessage))
        {
            return;
        }

        DiagnosticReceived?.Invoke(
            this,
            new EditorDiagnosticReceivedEventArgs(ReadDiagnosticLevel(message), diagnosticMessage));
    }

    private async Task<IBlockSuiteHostClient> WaitForHostAsync()
    {
        await WaitUntilReadyAsync();
        if (host is null)
        {
            logger?.LogError("BlockSuite host readiness completed without an active host.");
            throw new InvalidOperationException("The BlockSuite host is not ready.");
        }

        return host;
    }

    private async Task InvokeHostAsync(Func<IBlockSuiteHostClient, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        await action(blockSuiteHost);
    }

    private async Task<TResult> InvokeHostAsync<TResult>(Func<IBlockSuiteHostClient, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        IBlockSuiteHostClient blockSuiteHost = await WaitForHostAsync();
        return await action(blockSuiteHost);
    }

    private async Task InvokeAsyncHandlers<TEventArgs>(
        MulticastDelegate? handlers,
        TEventArgs args)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            if (handler is Func<object?, TEventArgs, Task> asyncHandler)
            {
                await asyncHandler(this, args);
            }
        }
    }

    private static string ReadDiagnosticMessage(JsonElement messageElement)
    {
        return messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString() ?? string.Empty
            : messageElement.ToString();
    }

    private static EditorDiagnosticLevel ReadDiagnosticLevel(JsonElement message)
    {
        if (!message.TryGetProperty("level", out JsonElement levelElement)
            || levelElement.ValueKind != JsonValueKind.String)
        {
            return EditorDiagnosticLevel.Information;
        }

        return levelElement.GetString()?.Trim().ToLowerInvariant() switch
        {
            "error" => EditorDiagnosticLevel.Error,
            "warn" or "warning" => EditorDiagnosticLevel.Warning,
            _ => EditorDiagnosticLevel.Information
        };
    }
}

internal enum EditorDiagnosticLevel
{
    Information,
    Warning,
    Error
}

internal sealed class EditorDiagnosticReceivedEventArgs : EventArgs
{
    internal EditorDiagnosticReceivedEventArgs(EditorDiagnosticLevel level, string message)
    {
        Level = level;
        Message = message;
    }

    internal EditorDiagnosticLevel Level { get; }

    internal string Message { get; }
}
