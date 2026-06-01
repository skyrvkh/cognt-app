using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class EditorDocumentRequestController
{
    private readonly MainViewModel viewModel;
    private readonly EditorView editorView;
    private readonly EditorDerivedDataRefreshController refreshes;
    private readonly Func<string, string, string, Task> openImageOcclusionForImageAsync;
    private readonly ILogger? logger;

    internal EditorDocumentRequestController(
        MainViewModel viewModel,
        EditorView editorView,
        EditorDerivedDataRefreshController refreshes,
        Func<string, string, string, Task> openImageOcclusionForImageAsync,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(refreshes);
        ArgumentNullException.ThrowIfNull(openImageOcclusionForImageAsync);

        this.viewModel = viewModel;
        this.editorView = editorView;
        this.refreshes = refreshes;
        this.openImageOcclusionForImageAsync = openImageOcclusionForImageAsync;
        this.logger = logger;

        editorView.ContentChanged += EditorView_ContentChanged;
        editorView.CreateExtractRequested += EditorView_CreateExtractRequested;
        editorView.SaveClozeRequested += EditorView_SaveClozeRequested;
        editorView.OpenImageOcclusionRequested += EditorView_OpenImageOcclusionRequested;
    }

    private async void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        string? sourcePath = e.SourcePath;

        await EditorHostRequestSupport.InvokeAsync(
            () => viewModel.CreateExtractNoteAsync(
                sourcePath,
                e.SelectedContent,
                e.SelectedPlainText),
            createdPath => editorView.PostExtractResult(ok: true, createdPath),
            ex =>
            {
                logger?.LogError(ex, "Editor extract note failed. SourcePath={SourcePath}", sourcePath);
                editorView.PostExtractResult(ok: false, error: ex.Message);
            });
    }

    private void EditorView_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        EditorHostRequestSupport.Invoke(
            () =>
            {
                viewModel.SaveClozeDraft(
                    e.SourcePath,
                    e.BlockId,
                    e.SelectedPlainText,
                    e.ClozeText,
                    e.Content,
                    e.CardContextId,
                    e.SlotId,
                    e.DisplayClozeIndex);
                refreshes.RefreshDocumentLinks(e.Content);
                return true;
            },
            _ => editorView.PostSaveResult(ok: true),
            ex =>
            {
                logger?.LogError(ex, "Editor cloze save failed. SourcePath={SourcePath} BlockId={BlockId}", e.SourcePath, e.BlockId);
                editorView.PostSaveResult(ok: false, error: ex.Message);
            });
    }

    private async void EditorView_OpenImageOcclusionRequested(object? sender, OpenImageOcclusionRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            () => openImageOcclusionForImageAsync(
                e.ImageBlockId,
                e.ImageRelPath,
                e.Content),
            () => { },
            ex => logger?.LogError(
                ex,
                "Editor image occlusion open failed. ImageBlockId={ImageBlockId} ImageRelPath={ImageRelPath}",
                e.ImageBlockId,
                e.ImageRelPath));
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        EditorHostRequestSupport.Invoke(
            () =>
            {
                viewModel.SaveNoteIfChanged(content);
                refreshes.RefreshDocumentLinks(content);
                return true;
            },
            _ => { },
            ex =>
            {
                logger?.LogError(ex, "Editor content save failed. ContentLength={ContentLength}", content.Length);
                editorView.PostSaveResult(ok: false, error: ex.Message);
            });
    }
}
