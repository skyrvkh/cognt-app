using Cognt.Modules.PersonalKnowledgeManagement.Domain;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal interface ITreeRepository : IDisposable
{
    IReadOnlyList<KnowledgeTreeItemRow> LoadRootItems();

    IReadOnlyList<KnowledgeTreeItemRow> LoadChildren(long parentId);

    bool HasAnyItems();

    KnowledgeTreeItemRow? GetItem(long itemId);

    IReadOnlyList<KnowledgeTreeItemRow> LoadNoteItems();

    IReadOnlyList<KnowledgeTreeItemRow> SearchNoteItems(string query, int limit);

    KnowledgeTreeItemRow? FindNoteByFileRelPath(string fileRelPath);

    KnowledgeTreeItemRow? FindItemByFileRelPath(
        string fileRelPath,
        long? parentId,
        KnowledgeTreeItemKind kind,
        bool includeDeleted);

    long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreateNote(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreatePdfDocument(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    void RenameItem(long itemId, string title);

    void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId);

    void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId);

    void DeleteItem(long itemId);

    void RewriteFileRelPathPrefix(string oldPrefix, string newPrefix);
}
