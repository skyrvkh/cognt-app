using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class EditorLinkRequestController
{
    private readonly MainViewModel viewModel;
    private readonly EditorView editorView;
    private readonly Func<string, bool, Task<bool>> openNoteAsync;
    private readonly ILogger? logger;

    internal EditorLinkRequestController(
        MainViewModel viewModel,
        EditorView editorView,
        Func<string, bool, Task<bool>> openNoteAsync,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(openNoteAsync);

        this.viewModel = viewModel;
        this.editorView = editorView;
        this.openNoteAsync = openNoteAsync;
        this.logger = logger;

        editorView.MentionSearchRequested += EditorView_MentionSearchRequested;
        editorView.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        editorView.OpenMentionRequested += EditorView_OpenMentionRequested;
        editorView.CreateLinkedNoteRequested += EditorView_CreateLinkedNoteRequested;
    }

    private void EditorView_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        EditorHostRequestSupport.Invoke(
            () => viewModel.SearchMentionTargets(e.Query, e.Limit),
            e.Complete,
            ex =>
            {
                logger?.LogError(ex, "Editor mention search failed. Query={Query} Limit={Limit}", e.Query, e.Limit);
                e.Complete([]);
            });
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        EditorHostRequestSupport.Invoke(
            () => viewModel.ReadMentionPreview(e.RelativePath, e.MaxCharacters),
            preview =>
            {
                editorView.PostMentionPreviewResult(
                    e.RequestId,
                    e.RelativePath,
                    e.Mode,
                    ok: true,
                    documentContent: preview);
            },
            ex =>
            {
                logger?.LogError(ex, "Editor mention preview failed. RequestId={RequestId} RelativePath={RelativePath}", e.RequestId, e.RelativePath);
                editorView.PostMentionPreviewResult(
                    e.RequestId,
                    e.RelativePath,
                    e.Mode,
                    ok: false,
                    error: ex.Message);
            });
    }

    private async void EditorView_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        await EditorHostRequestSupport.InvokeAsync(
            async () =>
            {
                viewModel.SaveMentionSource(e.SourcePath, e.Content);
                if (viewModel.TryOpenPdfDocumentByRelativePath(e.RelativePath))
                {
                    return true;
                }

                string targetPath = viewModel.ResolveMentionPath(e.RelativePath);
                return await openNoteAsync(targetPath, true);
            },
            opened => editorView.PostMentionOpenResult(
                ok: opened,
                error: opened ? null : "The mentioned note could not be opened."),
            ex =>
            {
                logger?.LogError(ex, "Editor mention open failed. RelativePath={RelativePath}", e.RelativePath);
                editorView.PostMentionOpenResult(ok: false, error: ex.Message);
            });
    }

    private void EditorView_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        EditorHostRequestSupport.Invoke(
            () => viewModel.CreateLinkedNote(e.SourcePath),
            e.Complete,
            ex =>
            {
                logger?.LogError(ex, "Editor create linked note failed. SourcePath={SourcePath}", e.SourcePath);
                e.Complete(CreateLinkedNoteResult.Fail(ex.Message));
            });
    }
}
