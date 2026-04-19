using CogntApp.Models.Tree;

namespace CogntApp.Services;

public interface ITreeRepository : IDisposable
{
    IReadOnlyList<TreeItemRow> LoadRootItems();

    IReadOnlyList<TreeItemRow> LoadChildren(long parentId);

    IReadOnlyList<TreeItemRow> LoadNoteItems();

    IReadOnlyList<TreeItemRow> SearchNoteItems(string query, int limit);

    long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreateNote(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId);

    void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId);

    bool HasAnyItems();

    TreeItemRow? GetItem(long itemId);

    TreeItemRow? FindNoteByFileRelPath(string fileRelPath);

    void RenameItem(long itemId, string title);

    void DeleteItem(long itemId);
}
