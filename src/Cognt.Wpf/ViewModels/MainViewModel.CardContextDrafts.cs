using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void RecomputeCardContextDraftStatuses(string relativePath, string content)
    {
        string? existingContent = noteService.ReadCardContextDrafts(openCollectionPath, relativePath);
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return;
        }

        CardContextDraftNoteDocument document = CardContextDraftDocumentCodec.Read(existingContent, relativePath);
        PersistCardContextDraftDocument(relativePath, content, document);
    }

    private void ReloadCurrentNoteToCardQueue(string relativePath, string content)
    {
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
        CardContextDraftNoteDocument refreshed = CardContextSnapshotFactory.RefreshContextStatuses(
            relativePath,
            noteContent,
            document);

        noteService.SaveCardContextDrafts(
            openCollectionPath,
            relativePath,
            refreshed.Contexts.Count == 0
                ? null
                : CardContextDraftDocumentCodec.Write(refreshed));
    }

    private static CardContextDraftNoteDocument ReplaceDraftContext(
        CardContextDraftNoteDocument document,
        CardContext updatedContext)
    {
        CardContext[] contexts = document.Contexts.ToArray();
        int contextIndex = Array.FindIndex(
            contexts,
            context => string.Equals(context.Id, updatedContext.Id, StringComparison.Ordinal));

        if (contextIndex < 0)
        {
            return document with { Contexts = contexts.Concat([updatedContext]).ToArray() };
        }

        contexts[contextIndex] = updatedContext;
        return document with { Contexts = contexts };
    }

    private static CardContext ResolveRequestedDraftContext(
        CardContextDraftNoteDocument document,
        string? cardContextId)
    {
        if (string.IsNullOrWhiteSpace(cardContextId))
        {
            return document.Contexts[0];
        }

        return document.Contexts.FirstOrDefault(context =>
                string.Equals(context.Id, cardContextId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The selected card context no longer exists.");
    }
}
