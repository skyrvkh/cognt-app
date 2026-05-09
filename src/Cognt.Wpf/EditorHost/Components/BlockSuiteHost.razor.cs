using System.Text.Json;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Cognt.Wpf.EditorHost.Components;

public partial class BlockSuiteHost : IAsyncDisposable, IBlockSuiteHostClient
{
    private const string EditorModulePath = "./editorweb/assets/blazor-blocksuite.js";

    private DotNetObjectReference<BlockSuiteHost>? dotNetReference;
    private IJSObjectReference? module;
    private int instanceId;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

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
            Bridge.RegisterStartupFailure(ex);
            System.Diagnostics.Debug.WriteLine($"BlockSuite host startup failed: {ex}");
        }
    }

    async Task IBlockSuiteHostClient.LoadNoteAsync(string? path, string title, string content, string? collectionRoot)
    {
        await RequireModule().InvokeVoidAsync("loadNote", instanceId, path, title, content, collectionRoot);
    }

    async Task IBlockSuiteHostClient.ActivateEditorAsync()
    {
        await RequireModule().InvokeVoidAsync("activateEditor", instanceId);
    }

    async Task IBlockSuiteHostClient.SetReadOnlyModeAsync(bool isReadOnly)
    {
        await RequireModule().InvokeVoidAsync("setReadOnlyMode", instanceId, isReadOnly);
    }

    async Task IBlockSuiteHostClient.FocusEditorAsync()
    {
        await RequireModule().InvokeVoidAsync("focusEditor", instanceId);
    }

    async Task IBlockSuiteHostClient.CreateClozeFromSelectionAsync(
        string? cardContextId,
        string? slotId,
        int? displayClozeIndex)
    {
        await RequireModule().InvokeVoidAsync(
            "createClozeFromSelection",
            instanceId,
            cardContextId,
            slotId,
            displayClozeIndex);
    }

    async Task<SelectedImageBlockReadModel?> IBlockSuiteHostClient.GetSelectedImageBlockAsync()
    {
        return await RequireModule().InvokeAsync<SelectedImageBlockReadModel?>(
            "getSelectedImageBlock",
            instanceId);
    }

    async Task<SelectedCardContextBlocksReadModel?> IBlockSuiteHostClient.GetSelectedCardContextBlocksAsync()
    {
        return await RequireModule().InvokeAsync<SelectedCardContextBlocksReadModel?>(
            "getSelectedCardContextBlocks",
            instanceId);
    }

    async Task IBlockSuiteHostClient.ShowSaveResultAsync(bool ok, string? error)
    {
        await RequireModule().InvokeVoidAsync("showSaveResult", instanceId, ok, error);
    }

    async Task IBlockSuiteHostClient.ShowExtractResultAsync(bool ok, string? createdPath, string? error)
    {
        await RequireModule().InvokeVoidAsync("showExtractResult", instanceId, ok, createdPath, error);
    }

    async Task IBlockSuiteHostClient.SetMentionTargetsAsync(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        IEnumerable<object> payload = targets.Select(CreateMentionTargetPayload);

        await RequireModule().InvokeVoidAsync("setMentionTargets", instanceId, payload);
    }

    async Task IBlockSuiteHostClient.SetDocumentLinksAsync(KnowledgeDocumentLinksReadModel links)
    {
        var payload = new
        {
            incoming = links.Incoming.Select(CreateDocumentLinkPayload),
            outgoing = links.Outgoing.Select(CreateDocumentLinkPayload),
        };

        await RequireModule().InvokeVoidAsync("setDocumentLinks", instanceId, payload);
    }

    async Task IBlockSuiteHostClient.HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent,
        string? error)
    {
        await RequireModule().InvokeVoidAsync(
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
        await RequireModule().InvokeVoidAsync("handleMentionOpenResult", instanceId, ok, error);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (module is not null && instanceId != 0)
            {
                await module.InvokeVoidAsync("disposeBlockSuite", instanceId);
            }

            if (module is not null)
            {
                await module.DisposeAsync();
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or ObjectDisposedException)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite host disposal skipped: {ex.Message}");
        }
        finally
        {
            Bridge.Unregister(this);
            dotNetReference?.Dispose();
        }
    }

    private IJSObjectReference RequireModule()
    {
        return module ?? throw new InvalidOperationException("The BlockSuite JavaScript module is not ready.");
    }
}
