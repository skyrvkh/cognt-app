using System.Text.Json;
using CogntApp.Models.Markdown;
using CogntApp.Services;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;

namespace CogntApp.Components;

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

    private ElementReference MentionPreviewElement { get; set; }

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
                StatusElement,
                MentionPreviewElement,
                dotNetReference);

            Bridge.Register(this);
        }
        catch (Exception ex)
        {
            Bridge.RegisterStartupFailure(ex);
            System.Diagnostics.Debug.WriteLine($"BlockSuite host startup failed: {ex}");
        }
    }

    public async Task LoadNoteAsync(string? path, string title, string content)
    {
        await RequireModule().InvokeVoidAsync("loadNote", instanceId, path, title, content);
    }

    public async Task LoadCardAsync(string content)
    {
        await RequireModule().InvokeVoidAsync("loadCard", instanceId, content);
    }

    public async Task BeginCardRenderAsync()
    {
        await RequireModule().InvokeVoidAsync("beginCardRender", instanceId);
    }

    public async Task ActivateEditorAsync()
    {
        await RequireModule().InvokeVoidAsync("activateEditor", instanceId);
    }

    public async Task FocusEditorAsync()
    {
        await RequireModule().InvokeVoidAsync("focusEditor", instanceId);
    }

    public async Task ShowSaveResultAsync(bool ok, string? error)
    {
        await RequireModule().InvokeVoidAsync("showSaveResult", instanceId, ok, error);
    }

    public async Task ShowExtractResultAsync(bool ok, string? createdPath, string? error)
    {
        await RequireModule().InvokeVoidAsync("showExtractResult", instanceId, ok, createdPath, error);
    }

    public async Task SetMentionTargetsAsync(IEnumerable<MentionTarget> targets)
    {
        var payload = targets.Select(target => new
        {
            relativePath = target.RelativePath,
            title = target.Title,
            fileName = target.FileName,
            recentRank = target.RecentRank,
        });

        await RequireModule().InvokeVoidAsync("setMentionTargets", instanceId, payload);
    }

    public async Task HandleMentionPreviewResultAsync(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? title,
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
            title,
            documentContent,
            error);
    }

    public async Task HandleMentionOpenResultAsync(bool ok, string? error)
    {
        await RequireModule().InvokeVoidAsync("handleMentionOpenResult", instanceId, ok, error);
    }

    [JSInvokable]
    public IReadOnlyList<object> HandleSearchMentionTargets(string sourcePath, string query, int limit)
    {
        return Bridge.RaiseMentionSearch(sourcePath, query, limit)
            .Select(target => new
            {
                relativePath = target.RelativePath,
                title = target.Title,
                fileName = target.FileName,
                recentRank = target.RecentRank,
            })
            .Cast<object>()
            .ToArray();
    }

    [JSInvokable]
    public void HandleContentChanged(string content)
    {
        Bridge.RaiseContentChanged(content);
    }

    [JSInvokable]
    public void HandleCreateExtract(string sourcePath, string selectedContent, string selectedPlainText)
    {
        Bridge.RaiseCreateExtract(sourcePath, selectedContent, selectedPlainText);
    }

    [JSInvokable]
    public void HandleSaveCloze(
        string sourcePath,
        string selectedPlainText,
        string clozeText,
        string content)
    {
        Bridge.RaiseSaveCloze(sourcePath, selectedPlainText, clozeText, content);
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
    public void HandleHostMessage(JsonElement message)
    {
        Bridge.RaiseHostMessage(message);
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

