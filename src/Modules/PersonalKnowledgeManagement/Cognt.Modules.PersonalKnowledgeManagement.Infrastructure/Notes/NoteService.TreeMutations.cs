using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    long INoteService.CreateFolder(
        string collectionId,
        long? parentId,
        string title,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        long itemId = repository.CreateFolder(parentId, title, previousSiblingId, nextSiblingId);
        return itemId;
    }

    void INoteService.MoveItem(
        string collectionId,
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        KnowledgeTreeItemRow item = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");

        if (item.ParentId == newParentId)
        {
            repository.ReorderItem(itemId, previousSiblingId, nextSiblingId);
            return;
        }

        repository.MoveItem(itemId, newParentId, previousSiblingId, nextSiblingId);
        InvalidateNoteCatalogCache(GetExistingCollectionRoot(collectionId));
    }

    void INoteService.RenameItem(string collectionId, long itemId, string title)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        _ = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
        repository.RenameItem(itemId, title);
        InvalidateNoteCatalogCache(GetExistingCollectionRoot(collectionId));
    }

    IReadOnlyList<string> INoteService.DeleteItem(string collectionId, long itemId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        _ = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
        IReadOnlyList<string> deletedNotePaths = CollectDeletedNotePaths(root, repository, itemId);

        repository.DeleteItem(itemId);

        foreach (string notePath in deletedNotePaths)
        {
            TryDeleteFile(notePath, logger);
        }

        DeleteCardContextDrafts(
            root,
            deletedNotePaths.Select(path => NormalizeRelativePath(Path.GetRelativePath(root, path))));

        InvalidateNoteCatalogCache(root);

        return deletedNotePaths
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(root, path)))
            .ToArray();
    }

    IReadOnlyList<string> INoteService.PreviewDeleteItemNotePaths(string collectionId, long itemId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        return CollectDeletedNotePaths(root, repository, itemId)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(root, path)))
            .ToArray();
    }

    void INoteService.SaveNote(string collectionId, string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(collectionId) || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string root = GetExistingCollectionRoot(collectionId);
        string notePath = ResolveExistingRelativeNotePathInRoot(root, normalizedRelativePath);
        File.WriteAllText(notePath, content, Encoding.UTF8);
        string resolvedRelativePath = NormalizeRelativePath(Path.GetRelativePath(root, notePath));
        SyncSavedNoteAttachmentReferences(root, resolvedRelativePath, content);
        InvalidateNoteCatalogCache(root);
        TryRefreshSavedNoteMentionIndex(notePath, content);
    }
}
