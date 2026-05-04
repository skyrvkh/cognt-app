using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    public long CreateFolder(
        string collectionId,
        long? parentId,
        string title,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        return repository.CreateFolder(parentId, title, previousSiblingId, nextSiblingId);
    }

    public void MoveItem(
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

    public void RenameItem(string collectionId, long itemId, string title)
    {
        ITreeRepository repository = OpenTreeRepositoryForCollectionId(collectionId);
        repository.RenameItem(itemId, title);
        InvalidateNoteCatalogCache(GetExistingCollectionRoot(collectionId));
    }

    public IReadOnlyList<string> DeleteItem(string collectionId, long itemId)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        IReadOnlyList<string> deletedNotePaths = CollectDeletedNotePaths(root, repository, itemId);

        repository.DeleteItem(itemId);

        foreach (string notePath in deletedNotePaths)
        {
            TryDeleteFile(notePath);
        }

        DeleteCardContextDrafts(
            root,
            deletedNotePaths.Select(path => NormalizeRelativePath(Path.GetRelativePath(root, path))));

        InvalidateNoteCatalogCache(root);

        return deletedNotePaths
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(root, path)))
            .ToArray();
    }

    public void SaveNote(string collectionId, string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(collectionId) || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        string root = GetExistingCollectionRoot(collectionId);
        string notePath = ResolveExistingRelativeNotePathInRoot(root, relativePath);
        File.WriteAllText(notePath, content, Encoding.UTF8);
        InvalidateNoteCatalogCache(root);
        TryRefreshSavedNoteMentionIndex(notePath, content);
    }
}
