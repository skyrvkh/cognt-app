using Cognt.Modules.PersonalKnowledgeManagement.Domain;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

public sealed class PkmTreeRepository : IDisposable
{
    private readonly ITreeRepository repository;

    public PkmTreeRepository(string databasePath)
    {
        repository = new SqliteTreeRepository(databasePath);
    }

    public IReadOnlyList<KnowledgeTreeItemRow> LoadRootItems()
    {
        return repository.LoadRootItems();
    }

    public IReadOnlyList<KnowledgeTreeItemRow> LoadChildren(long parentId)
    {
        return repository.LoadChildren(parentId);
    }

    public IReadOnlyList<KnowledgeTreeItemRow> LoadNoteItems()
    {
        return repository.LoadNoteItems();
    }

    public IReadOnlyList<KnowledgeTreeItemRow> SearchNoteItems(string query, int limit)
    {
        return repository.SearchNoteItems(query, limit);
    }

    public bool HasAnyItems()
    {
        return repository.HasAnyItems();
    }

    public KnowledgeTreeItemRow? GetItem(long itemId)
    {
        return repository.GetItem(itemId);
    }

    public KnowledgeTreeItemRow? FindNoteByFileRelPath(string fileRelPath)
    {
        return repository.FindNoteByFileRelPath(fileRelPath);
    }

    public KnowledgeTreeItemRow? FindItemByFileRelPath(
        string fileRelPath,
        long? parentId,
        KnowledgeTreeItemKind kind,
        bool includeDeleted)
    {
        return repository.FindItemByFileRelPath(fileRelPath, parentId, kind, includeDeleted);
    }

    public long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null)
    {
        return repository.CreateFolder(parentId, title, previousSiblingId, nextSiblingId);
    }

    public long CreateNote(
        long? parentId,
        string title,
        string fileRelPath,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        return repository.CreateNote(parentId, title, fileRelPath, previousSiblingId, nextSiblingId);
    }

    public long CreatePdfDocument(
        long? parentId,
        string title,
        string fileRelPath,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        return repository.CreatePdfDocument(parentId, title, fileRelPath, previousSiblingId, nextSiblingId);
    }

    public void RenameItem(long itemId, string title)
    {
        repository.RenameItem(itemId, title);
    }

    public void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId)
    {
        repository.MoveItem(itemId, newParentId, previousSiblingId, nextSiblingId);
    }

    public void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId)
    {
        repository.ReorderItem(itemId, previousSiblingId, nextSiblingId);
    }

    public void DeleteItem(long itemId)
    {
        repository.DeleteItem(itemId);
    }

    public void Dispose()
    {
        repository.Dispose();
    }
}
