using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private readonly ConcurrentDictionary<string, string> transientNoteToCardDrafts = new(StringComparer.OrdinalIgnoreCase);
    private int noteToCardQueueRefreshVersion;

    private void ScheduleCurrentNoteToCardQueueRefresh(string relativePath, string content)
    {
        string collectionPath = openCollectionPath;
        int refreshVersion = ++noteToCardQueueRefreshVersion;

        _ = Task.Run(() => RefreshCardContextDraftState(collectionPath, relativePath, content))
            .ContinueWith(
                task => ApplyScheduledNoteToCardQueueRefresh(refreshVersion, collectionPath, relativePath, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void ReloadCurrentNoteToCardQueue(
        string relativePath,
        string content,
        bool discardSavedDraft = false)
    {
        noteToCardQueueRefreshVersion++;
        if (discardSavedDraft)
        {
            ClearNoteToCardDraftState(openCollectionPath, relativePath);
            CurrentNoteToCardQueue = null;
            return;
        }

        string? existingContent = ReadNoteToCardDraftContent(openCollectionPath, relativePath);
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
        string? existingContent = ReadNoteToCardDraftContent(collectionPath, relativePath);
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

        NoteToCardQueueReadModel result = noteToCardQueueQuery.Execute(
            new NoteToCardQueueQueryRequest(
                collectionPath,
                relativePath,
                content,
                refreshed));
        return result;
    }

    private void ApplyScheduledNoteToCardQueueRefresh(
        int refreshVersion,
        string collectionPath,
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
                logger?.LogError(
                    task.Exception.GetBaseException(),
                    "Scheduled note-to-card queue refresh failed. CollectionPath={CollectionPath} Version={Version} RelativePath={RelativePath}",
                    collectionPath,
                    refreshVersion,
                    relativePath);
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
                logger?.LogWarning(
                    "Collection image path rejected because URI is not a file. CollectionPath={CollectionPath} ImageRelPath={ImageRelPath} UriScheme={UriScheme}",
                    openCollectionPath,
                    imageRelPath,
                    absoluteUri.Scheme);
                throw new InvalidOperationException("Only collection-local image files can be masked.");
            }

            string relativeFilePath = GetRequiredRelativeCollectionPath(absoluteUri.LocalPath);
            return relativeFilePath;
        }

        if (Path.IsPathRooted(candidate))
        {
            string relativeRootedPath = GetRequiredRelativeCollectionPath(candidate);
            return relativeRootedPath;
        }

        string fullPath = ResolveCollectionFilePath(openCollectionPath, candidate);
        _ = GetRequiredRelativeCollectionPath(fullPath);
        string normalized = candidate.Replace('\\', '/').Trim('/');
        return normalized;
    }

    private string GetRequiredRelativeCollectionPath(string fullPath)
    {
        string root = Path.GetFullPath(openCollectionPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = string.Concat(root, Path.DirectorySeparatorChar);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        if (!normalizedFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning(
                "Relative collection path resolution failed because file is outside collection. CollectionRoot={CollectionRoot} FullPath={FullPath}",
                root,
                normalizedFullPath);
            throw new InvalidOperationException("The selected image must be inside the current collection.");
        }

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, normalizedFullPath));
        return relativePath;
    }

    private CardContextDraftNoteDocument ReadCardContextDraftDocument(string relativePath)
    {
        CardContextDraftNoteDocument document = CardContextDraftDocumentCodec.Read(
            ReadNoteToCardDraftContent(openCollectionPath, relativePath),
            relativePath);
        return document;
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

        WriteNoteToCardDraftContent(
            collectionPath,
            relativePath,
            refreshed.Contexts.Count == 0
                ? null
                : CardContextDraftDocumentCodec.Write(refreshed));

        return refreshed;
    }

    private string? ReadNoteToCardDraftContent(string collectionPath, string relativePath)
    {
        bool found = transientNoteToCardDrafts.TryGetValue(
            CreateNoteToCardDraftKey(collectionPath, relativePath),
            out string? content);
        return found ? content : null;
    }

    private void WriteNoteToCardDraftContent(string collectionPath, string relativePath, string? content)
    {
        string key = CreateNoteToCardDraftKey(collectionPath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            transientNoteToCardDrafts.TryRemove(key, out _);
        }
        else
        {
            transientNoteToCardDrafts[key] = content;
        }

        DeletePersistentNoteToCardDraft(collectionPath, relativePath);
    }

    private void ClearNoteToCardDraftState(string collectionPath, string relativePath)
    {
        transientNoteToCardDrafts.TryRemove(CreateNoteToCardDraftKey(collectionPath, relativePath), out _);
        DeletePersistentNoteToCardDraft(collectionPath, relativePath);
    }

    private void DeletePersistentNoteToCardDraft(string collectionPath, string relativePath)
    {
        noteService.SaveCardContextDrafts(collectionPath, relativePath, content: null);
    }

    private static string CreateNoteToCardDraftKey(string collectionPath, string relativePath)
    {
        return $"{Path.GetFullPath(collectionPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}|{NormalizeRelativePath(relativePath)}";
    }

    private static CardContextDraftNoteDocument ReplaceDraftContext(
        CardContextDraftNoteDocument document,
        CardContext updatedContext)
    {
        return CardContextDraftDocumentEditor.ReplaceContext(document, updatedContext);
    }

    private static CardContext ResolveRequestedDraftContext(
        CardContextDraftNoteDocument document,
        string? cardContextId)
    {
        return CardContextDraftDocumentEditor.ResolveRequestedContext(document, cardContextId);
    }
}
