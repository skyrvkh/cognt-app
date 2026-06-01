using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Editor;

internal sealed class EditorDerivedDataRefreshController
{
    private readonly MainViewModel viewModel;
    private readonly EditorView editorView;
    private readonly IShellDialogService dialogs;
    private readonly ILogger? logger;
    private int mentionTargetsRefreshRequestId;
    private int documentLinksRefreshVersion;

    internal EditorDerivedDataRefreshController(
        MainViewModel viewModel,
        EditorView editorView,
        IShellDialogService dialogs,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.editorView = editorView;
        this.dialogs = dialogs;
        this.logger = logger;
    }

    internal async void QueueMentionTargetsRefresh()
    {
        int requestId = ++mentionTargetsRefreshRequestId;

        try
        {
            IReadOnlyList<KnowledgeMentionTargetReadModel> targets = await Task.Run(viewModel.ListMentionTargets);

            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            editorView.PostMentionTargets(targets);
        }
        catch (Exception ex)
        {
            if (requestId != mentionTargetsRefreshRequestId)
            {
                return;
            }

            logger?.LogError(ex, "Mention targets refresh failed. RequestId={RequestId}", requestId);
            dialogs.ShowWarning(
                "Mentions",
                $"Mention targets could not be refreshed: {ex.Message}");
        }
    }

    internal void RefreshDocumentLinksFromCurrentContent()
    {
        RefreshDocumentLinks(editorView.GetContent());
    }

    internal void RefreshDocumentLinks(string currentContent)
    {
        string collectionPath = viewModel.OpenCollectionPath;
        string relativePath = viewModel.CurrentNoteRelativePath ?? string.Empty;
        int refreshVersion = ++documentLinksRefreshVersion;

        _ = Task.Run(() => viewModel.ReadDocumentLinks(collectionPath, relativePath, currentContent))
            .ContinueWith(
                task => PostDocumentLinksResult(refreshVersion, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void PostDocumentLinksResult(
        int refreshVersion,
        Task<KnowledgeDocumentLinksReadModel> task)
    {
        void PostIfCurrent()
        {
            if (refreshVersion != documentLinksRefreshVersion)
            {
                return;
            }

            if (task.IsCompletedSuccessfully)
            {
                editorView.PostDocumentLinks(task.Result);
                return;
            }

            if (task.Exception is not null)
            {
                logger?.LogError(task.Exception.GetBaseException(), "Document links refresh failed. Version={Version}", refreshVersion);
            }

            editorView.PostDocumentLinks(new KnowledgeDocumentLinksReadModel([], []));
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(PostIfCurrent);
            return;
        }

        PostIfCurrent();
    }
}
