using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using CogntApp.Models.Tree;
using ModulePkmTreeRepository = Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence.PkmTreeRepository;

namespace CogntApp.Services;

public sealed class PkmTreeRepository : ITreeRepository
{
    private readonly ModulePkmTreeRepository repository;

    public PkmTreeRepository(string databasePath)
    {
        repository = new ModulePkmTreeRepository(databasePath);
    }

    public IReadOnlyList<TreeItemRow> LoadRootItems()
    {
        return MapRows(repository.LoadRootItems());
    }

    public IReadOnlyList<TreeItemRow> LoadChildren(long parentId)
    {
        return MapRows(repository.LoadChildren(parentId));
    }

    public IReadOnlyList<TreeItemRow> LoadNoteItems()
    {
        return MapRows(repository.LoadNoteItems());
    }

    public IReadOnlyList<TreeItemRow> SearchNoteItems(string query, int limit)
    {
        return MapRows(repository.SearchNoteItems(query, limit));
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

    public void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId)
    {
        repository.MoveItem(itemId, newParentId, previousSiblingId, nextSiblingId);
    }

    public void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId)
    {
        repository.ReorderItem(itemId, previousSiblingId, nextSiblingId);
    }

    public bool HasAnyItems()
    {
        return repository.HasAnyItems();
    }

    public TreeItemRow? GetItem(long itemId)
    {
        KnowledgeTreeItemRow? item = repository.GetItem(itemId);
        return item is null ? null : MapRow(item);
    }

    public TreeItemRow? FindNoteByFileRelPath(string fileRelPath)
    {
        KnowledgeTreeItemRow? item = repository.FindNoteByFileRelPath(fileRelPath);
        return item is null ? null : MapRow(item);
    }

    public void RenameItem(long itemId, string title)
    {
        repository.RenameItem(itemId, title);
    }

    public void DeleteItem(long itemId)
    {
        repository.DeleteItem(itemId);
    }


    public void RenumberSiblings(long? parentId)
    {
    }

    public bool WouldCreateCycle(long movingItemId, long? newParentId)
    {
        return false;
    }

    public void Dispose()
    {
        repository.Dispose();
    }

    private static IReadOnlyList<TreeItemRow> MapRows(IReadOnlyList<KnowledgeTreeItemRow> rows)
    {
        return rows.Select(MapRow).ToArray();
    }

    private static TreeItemRow MapRow(KnowledgeTreeItemRow row)
    {
        return new TreeItemRow(
            row.Id,
            row.Uid,
            row.ParentId,
            MapKind(row.Kind),
            row.Title,
            row.Position,
            row.FileRelPath,
            row.ChildCount,
            row.IsDeleted,
            row.CreatedAt,
            row.UpdatedAt,
            row.FileSize,
            row.FileMtimeUtc,
            row.ContentHash);
    }

    private static TreeItemKind MapKind(KnowledgeTreeItemKind kind)
    {
        return kind switch
        {
            KnowledgeTreeItemKind.Root => TreeItemKind.Root,
            KnowledgeTreeItemKind.Trash => TreeItemKind.Trash,
            KnowledgeTreeItemKind.Note => TreeItemKind.Note,
            KnowledgeTreeItemKind.PdfDocument => TreeItemKind.PdfDocument,
            _ => TreeItemKind.Folder,
        };
    }
}
