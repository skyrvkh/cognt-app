using System.Text.Json;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Cognt.Wpf.EditorHost.Components;

public partial class BlockSuiteHost : IAsyncDisposable, IBlockSuiteHostClient
{
    private const string EditorModulePath = "./editorweb/assets/blazor-blocksuite.js";
    private static readonly TimeSpan DisposalOperationTimeout = TimeSpan.FromSeconds(2);

    private DotNetObjectReference<BlockSuiteHost>? dotNetReference;
    private IJSObjectReference? module;
    private int instanceId;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private ILogger<BlockSuiteHost> Logger { get; set; } = default!;

    [Parameter]
    public BlockSuiteHostBridge Bridge { get; set; } = default!;

    private ElementReference EditorHostElement { get; set; }

    private ElementReference DocumentLinksElement { get; set; }

    private ElementReference StatusElement { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            dotNetReference = DotNetObjectReference.Create(this);
            module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", EditorModulePath);
            instanceId = await module.InvokeAsync<int>(
                "mountBlockSuite",
                EditorHostElement,
                DocumentLinksElement,
                StatusElement,
                dotNetReference);
            Bridge.Register(this);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BlockSuiteHost startup failed.");
            Bridge.RegisterStartupFailure(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (module is not null && instanceId != 0)
            {
                await WaitForDisposalOperationAsync(
                    module.InvokeVoidAsync("disposeBlockSuite", instanceId).AsTask(),
                    "disposeBlockSuite");
            }

            if (module is not null)
            {
                await WaitForDisposalOperationAsync(
                    module.DisposeAsync().AsTask(),
                    "module.DisposeAsync");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "BlockSuiteHost disposal skipped. InstanceId={InstanceId}", instanceId);
        }
        finally
        {
            if (instanceId == 0)
            {
                Bridge.FailPendingStartup(new InvalidOperationException(
                    "The BlockSuite host was disposed before the JavaScript editor mounted."));
            }

            Bridge.Unregister(this);
            dotNetReference?.Dispose();
        }
    }

    [JSInvokable]
    public void HandleHostMessage(JsonElement message)
    {
        Bridge.RaiseHostMessage(message);
    }

    [JSInvokable]
    public void HandleContentChanged(string content)
    {
        Bridge.RaiseContentChanged(content);
    }

    [JSInvokable]
    public void HandleDocumentChanged()
    {
        Bridge.RaiseDocumentChanged();
    }

    [JSInvokable]
    public void HandleCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        Bridge.RaiseCreateExtract(sourcePath, selectedContent, selectedPlainText);
    }

    [JSInvokable]
    public void HandleSaveCloze(
        string sourcePath,
        string blockId,
        string selectedPlainText,
        string clozeText,
        string content,
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        Bridge.RaiseSaveCloze(
            sourcePath,
            blockId,
            selectedPlainText,
            clozeText,
            content,
            cardContextId,
            slotId,
            displayClozeIndex);
    }

    [JSInvokable]
    public void HandleOpenImageOcclusion(
        string sourcePath,
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        Bridge.RaiseOpenImageOcclusion(sourcePath, imageBlockId, imageRelPath, content);
    }

    [JSInvokable]
    public IReadOnlyList<object> HandleSearchMentionTargets(string sourcePath, string query, int limit)
    {
        return CreateMentionTargetPayloads(Bridge.RaiseMentionSearch(sourcePath, query, limit));
    }

    [JSInvokable]
    public void HandleRequestMentionPreview(
        string requestId,
        string sourcePath,
        string relativePath,
        string mode,
        int maxCharacters)
    {
        Bridge.RaiseMentionPreview(requestId, sourcePath, relativePath, mode, maxCharacters);
    }

    [JSInvokable]
    public void HandleOpenMention(string sourcePath, string relativePath, string content)
    {
        Bridge.RaiseOpenMention(sourcePath, relativePath, content);
    }

    [JSInvokable]
    public CreateLinkedNoteResult HandleCreateLinkedNote(string sourcePath, string viewMode)
    {
        return Bridge.RaiseCreateLinkedNote(sourcePath, viewMode);
    }

    [JSInvokable]
    public Task<AttachFileResult> HandleAttachFile(
        string sourcePath,
        string fileName,
        string contentBase64)
    {
        return Bridge.RaiseAttachFileAsync(sourcePath, fileName, contentBase64);
    }

    [JSInvokable]
    public Task<AttachFileResult> HandleAttachImageSource(
        string sourcePath,
        string imageSource,
        string fallbackFileName)
    {
        return Bridge.RaiseAttachImageSourceAsync(sourcePath, imageSource, fallbackFileName);
    }

    [JSInvokable]
    public async Task<string?> HandleReadNativeClipboardImage()
    {
        try
        {
            System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                return await dispatcher.InvokeAsync(ReadNativeClipboardImageCore).Task;
            }

            return ReadNativeClipboardImageCore();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Native clipboard image read failed.");
            return null;
        }
    }

    [JSInvokable]
    public Task<ReadAttachmentResult> HandleReadAttachment(
        string sourcePath,
        string imageSource)
    {
        return Bridge.RaiseReadAttachmentAsync(sourcePath, imageSource);
    }

    [JSInvokable]
    public Task<EditorBlobResult> HandleStoreEditorBlob(
        string sourcePath,
        string blobId,
        string contentType,
        string contentBase64)
    {
        return Bridge.RaiseStoreEditorBlobAsync(sourcePath, blobId, contentType, contentBase64);
    }

    [JSInvokable]
    public Task<EditorBlobReadResult> HandleReadEditorBlob(
        string sourcePath,
        string blobId)
    {
        return Bridge.RaiseReadEditorBlobAsync(sourcePath, blobId);
    }

    [JSInvokable]
    public Task<EditorBlobListResult> HandleListEditorBlobs(string sourcePath)
    {
        return Bridge.RaiseListEditorBlobsAsync(sourcePath);
    }

    async Task IBlockSuiteHostClient.LoadNoteAsync(string? path, string title, string content, string? collectionRoot)
    {
        await InvokeHostVoidAsync("loadNote", instanceId, path, title, content, collectionRoot);
    }

    async Task<string> IBlockSuiteHostClient.ExportContentAsync()
    {
        return await InvokeHostAsync<string>("exportContent", instanceId);
    }

    async Task IBlockSuiteHostClient.ActivateEditorAsync()
    {
        await InvokeHostVoidAsync("activateEditor", instanceId);
    }

    async Task IBlockSuiteHostClient.SetReadOnlyModeAsync(bool isReadOnly)
    {
        await InvokeHostVoidAsync("setReadOnlyMode", instanceId, isReadOnly);
    }

    async Task IBlockSuiteHostClient.ApplyThemeAsync(EditorTheme theme)
    {
        await InvokeHostVoidAsync("applyTheme", instanceId, theme);
    }

    async Task IBlockSuiteHostClient.FocusEditorAsync()
    {
        await InvokeHostVoidAsync("focusEditor", instanceId);
    }

    async Task IBlockSuiteHostClient.FocusBlockAsync(string blockId)
    {
        await InvokeHostVoidAsync("focusBlock", instanceId, blockId);
    }

    async Task<EditorCaretState?> IBlockSuiteHostClient.GetCaretStateAsync()
    {
        return await InvokeHostAsync<EditorCaretState?>("getCaretState", instanceId);
    }

    async Task IBlockSuiteHostClient.RestoreCaretStateAsync(EditorCaretState caret)
    {
        await InvokeHostVoidAsync("restoreCaretState", instanceId, caret);
    }

    async Task IBlockSuiteHostClient.CreateClozeFromSelectionAsync(
        string? cardContextId,
        string? slotId,
        int? displayClozeIndex)
    {
        await InvokeHostVoidAsync(
            "createClozeFromSelection",
            instanceId,
            cardContextId,
            slotId,
            displayClozeIndex);
    }

    async Task<SelectedImageBlockReadModel?> IBlockSuiteHostClient.GetSelectedImageBlockAsync()
    {
        return await InvokeHostAsync<SelectedImageBlockReadModel?>("getSelectedImageBlock", instanceId);
    }

    async Task<SelectedCardContextBlocksReadModel?> IBlockSuiteHostClient.GetSelectedCardContextBlocksAsync()
    {
        return await InvokeHostAsync<SelectedCardContextBlocksReadModel?>(
            "getSelectedCardContextBlocks",
            instanceId);
    }

    async Task IBlockSuiteHostClient.ShowSaveResultAsync(bool ok, string? error)
    {
        await InvokeHostVoidAsync("showSaveResult", instanceId, ok, error);
    }

    async Task IBlockSuiteHostClient.ShowExtractResultAsync(bool ok, string? createdPath, string? error)
    {
        await InvokeHostVoidAsync("showExtractResult", instanceId, ok, createdPath, error);
    }

    async Task IBlockSuiteHostClient.SetMentionTargetsAsync(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        await InvokeHostVoidAsync("setMentionTargets", instanceId, CreateMentionTargetPayloads(targets));
    }

    async Task IBlockSuiteHostClient.SetDocumentLinksAsync(KnowledgeDocumentLinksReadModel links)
    {
        await InvokeHostVoidAsync("setDocumentLinks", instanceId, CreateDocumentLinksPayload(links));
    }

    async Task IBlockSuiteHostClient.HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent,
        string? error)
    {
        await InvokeHostVoidAsync(
            "handleMentionPreviewResult",
            instanceId,
            requestId,
            relativePath,
            mode,
            ok,
            documentContent,
            error);
    }

    async Task IBlockSuiteHostClient.HandleMentionOpenResultAsync(bool ok, string? error)
    {
        await InvokeHostVoidAsync("handleMentionOpenResult", instanceId, ok, error);
    }

    private IJSObjectReference RequireModule()
    {
        return module ?? throw new InvalidOperationException("The BlockSuite JavaScript module is not ready.");
    }

    private async Task WaitForDisposalOperationAsync(Task task, string operation)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(DisposalOperationTimeout));
        if (completedTask != task)
        {
            Logger.LogWarning(
                "BlockSuiteHost disposal operation timed out. InstanceId={InstanceId} Operation={Operation} Timeout={Timeout}",
                instanceId,
                operation,
                DisposalOperationTimeout);
            _ = ObserveLateDisposalOperationAsync(task, operation);
            return;
        }

        await task;
    }

    private async Task ObserveLateDisposalOperationAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "BlockSuiteHost disposal operation failed after timeout. InstanceId={InstanceId} Operation={Operation}",
                instanceId,
                operation);
        }
    }

    private async Task InvokeHostVoidAsync(string method, params object?[] args)
    {
        await RequireModule().InvokeVoidAsync(method, args);
    }

    private async Task<TResult> InvokeHostAsync<TResult>(string method, params object?[] args)
    {
        return await RequireModule().InvokeAsync<TResult>(method, args);
    }

    private static string? ReadNativeClipboardImageCore()
    {
        if (!System.Windows.Clipboard.ContainsImage())
        {
            return null;
        }

        System.Windows.Media.Imaging.BitmapSource? image = System.Windows.Clipboard.GetImage();
        if (image is null)
        {
            return null;
        }

        System.Windows.Media.Imaging.PngBitmapEncoder encoder = new();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using System.IO.MemoryStream stream = new();
        encoder.Save(stream);
        return $"data:image/png;base64,{Convert.ToBase64String(stream.ToArray())}";
    }

    private static IReadOnlyList<object> CreateMentionTargetPayloads(
        IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        return targets.Select(CreateMentionTargetPayload).ToArray();
    }

    private static object CreateMentionTargetPayload(KnowledgeMentionTargetReadModel target)
    {
        return new
        {
            relativePath = target.RelativePath,
            title = target.Title,
        };
    }

    private static object CreateDocumentLinksPayload(KnowledgeDocumentLinksReadModel links)
    {
        return new
        {
            incoming = links.Incoming.Select(CreateDocumentLinkPayload).ToArray(),
            outgoing = links.Outgoing.Select(CreateDocumentLinkPayload).ToArray(),
        };
    }

    private static object CreateDocumentLinkPayload(KnowledgeDocumentLinkReadModel link)
    {
        return new
        {
            openRelativePath = link.OpenRelativePath,
            displayTitle = link.DisplayTitle,
            snippet = link.Snippet,
            matchedText = link.MatchedText,
            updatedAt = link.UpdatedAt,
        };
    }
}
