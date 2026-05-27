using System.IO;
using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private int noteToCardQueueRefreshVersion;

    private void ScheduleCurrentNoteToCardQueueRefresh(string relativePath, string content)
    {
        string collectionPath = openCollectionPath;
        int refreshVersion = ++noteToCardQueueRefreshVersion;

        _ = Task.Run(() => RefreshCardContextDraftState(collectionPath, relativePath, content))
            .ContinueWith(
                task => ApplyScheduledNoteToCardQueueRefresh(refreshVersion, relativePath, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void ReloadCurrentNoteToCardQueue(string relativePath, string content)
    {
        noteToCardQueueRefreshVersion++;
        string? existingContent = noteService.ReadCardContextDrafts(openCollectionPath, relativePath);
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            CurrentNoteToCardQueue = null;
            return;
        }

        CardContextDraftNoteDocument document = CardContextDraftDocumentCodec.Read(existingContent, relativePath);
        if (document.Contexts.Count == 0)
        {
            CurrentNoteToCardQueue = null;
            return;
        }

        CurrentNoteToCardQueue = noteToCardQueueQuery.Execute(
            new NoteToCardQueueQueryRequest(
                openCollectionPath,
                relativePath,
                content,
                document));
    }

    private NoteToCardQueueReadModel? RefreshCardContextDraftState(
        string collectionPath,
        string relativePath,
        string content)
    {
        string? existingContent = noteService.ReadCardContextDrafts(collectionPath, relativePath);
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return null;
        }

        CardContextDraftNoteDocument document = CardContextDraftDocumentCodec.Read(existingContent, relativePath);
        if (document.Contexts.Count == 0)
        {
            return null;
        }

        CardContextDraftNoteDocument refreshed = PersistCardContextDraftDocument(
            collectionPath,
            relativePath,
            content,
            document);

        return noteToCardQueueQuery.Execute(
            new NoteToCardQueueQueryRequest(
                collectionPath,
                relativePath,
                content,
                refreshed));
    }

    private void ApplyScheduledNoteToCardQueueRefresh(
        int refreshVersion,
        string relativePath,
        Task<NoteToCardQueueReadModel?> task)
    {
        void ApplyIfCurrent()
        {
            if (refreshVersion != noteToCardQueueRefreshVersion
                || !string.Equals(relativePath, CurrentNoteRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (task.IsCompletedSuccessfully)
            {
                CurrentNoteToCardQueue = task.Result;
            }
            else if (task.Exception is not null)
            {
                System.Diagnostics.Debug.WriteLine($"Note-to-card queue refresh failed: {task.Exception}");
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ApplyIfCurrent);
            return;
        }

        ApplyIfCurrent();
    }

    private string NormalizeCollectionImagePath(string imageRelPath)
    {
        string candidate = Uri.UnescapeDataString(imageRelPath)
            .Replace('\\', '/')
            .Trim()
            .Trim('<', '>')
            .TrimStart('/');

        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? absoluteUri))
        {
            if (!absoluteUri.IsFile)
            {
                throw new InvalidOperationException("Only collection-local image files can be masked.");
            }

            return GetRequiredRelativeCollectionPath(absoluteUri.LocalPath);
        }

        if (Path.IsPathRooted(candidate))
        {
            return GetRequiredRelativeCollectionPath(candidate);
        }

        string fullPath = ResolveCollectionFilePath(openCollectionPath, candidate);
        _ = GetRequiredRelativeCollectionPath(fullPath);
        return candidate.Replace('\\', '/').Trim('/');
    }

    private string GetRequiredRelativeCollectionPath(string fullPath)
    {
        string root = Path.GetFullPath(openCollectionPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = string.Concat(root, Path.DirectorySeparatorChar);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        if (!normalizedFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected image must be inside the current collection.");
        }

        return NormalizeRelativePath(Path.GetRelativePath(root, normalizedFullPath));
    }

    private CardContextDraftNoteDocument ReadCardContextDraftDocument(string relativePath)
    {
        return CardContextDraftDocumentCodec.Read(
            noteService.ReadCardContextDrafts(openCollectionPath, relativePath),
            relativePath);
    }

    private void PersistCardContextDraftDocument(
        string relativePath,
        string noteContent,
        CardContextDraftNoteDocument document)
    {
        _ = PersistCardContextDraftDocument(openCollectionPath, relativePath, noteContent, document);
    }

    private CardContextDraftNoteDocument PersistCardContextDraftDocument(
        string collectionPath,
        string relativePath,
        string noteContent,
        CardContextDraftNoteDocument document)
    {
        CardContextDraftNoteDocument refreshed = CardContextSnapshotFactory.RefreshContextStatuses(
            relativePath,
            noteContent,
            document);

        noteService.SaveCardContextDrafts(
            collectionPath,
            relativePath,
            refreshed.Contexts.Count == 0
                ? null
                : CardContextDraftDocumentCodec.Write(refreshed));

        return refreshed;
    }

    private static CardContextDraftNoteDocument ReplaceDraftContext(
        CardContextDraftNoteDocument document,
        CardContext updatedContext)
    {
        int contextIndex = FindDraftContextIndexById(document.Contexts, updatedContext.Id);

        if (contextIndex < 0)
        {
            return document with { Contexts = AppendDraftContext(document.Contexts, updatedContext) };
        }

        CardContext[] contexts = document.Contexts.ToArray();
        contexts[contextIndex] = updatedContext;
        return document with { Contexts = contexts };
    }

    private static CardContext[] AppendDraftContext(
        IReadOnlyList<CardContext> contexts,
        CardContext context)
    {
        CardContext[] nextContexts = new CardContext[contexts.Count + 1];
        for (int index = 0; index < contexts.Count; index++)
        {
            nextContexts[index] = contexts[index];
        }

        nextContexts[^1] = context;
        return nextContexts;
    }

    private static int FindDraftContextIndexById(IReadOnlyList<CardContext> contexts, string contextId)
    {
        for (int index = 0; index < contexts.Count; index++)
        {
            if (string.Equals(contexts[index].Id, contextId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static CardContext ResolveRequestedDraftContext(
        CardContextDraftNoteDocument document,
        string? cardContextId)
    {
        if (string.IsNullOrWhiteSpace(cardContextId))
        {
            return document.Contexts[0];
        }

        int contextIndex = FindDraftContextIndexById(document.Contexts, cardContextId);
        if (contextIndex < 0)
        {
            throw new InvalidOperationException("The selected card context no longer exists.");
        }

        return document.Contexts[contextIndex];
    }
}
