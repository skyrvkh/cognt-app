using Cognt.Modules.PersonalKnowledgeManagement.Domain;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal interface ITreeRepository : IDisposable
{
    IReadOnlyList<KnowledgeTreeItemRow> LoadRootItems();

    IReadOnlyList<KnowledgeTreeItemRow> LoadChildren(long parentId);

    IReadOnlyList<KnowledgeTreeItemRow> LoadNoteItems();

    IReadOnlyList<KnowledgeTreeItemRow> SearchNoteItems(string query, int limit);

    long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreateNote(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreatePdfDocument(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId);

    void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId);

    bool HasAnyItems();

    void RewriteFileRelPathPrefix(string oldPrefix, string newPrefix);

    KnowledgeTreeItemRow? GetItem(long itemId);

    KnowledgeTreeItemRow? FindNoteByFileRelPath(string fileRelPath);

    KnowledgeTreeItemRow? FindItemByFileRelPath(
        string fileRelPath,
        long? parentId,
        KnowledgeTreeItemKind kind,
        bool includeDeleted = false);

    bool HasDocumentMentions();

    IReadOnlyList<DocumentMentionRow> LoadDocumentMentionsBySource(string sourceDocumentId);

    IReadOnlyList<DocumentMentionRow> LoadDocumentMentionsByTarget(string targetDocumentId);

    IReadOnlyList<DocumentMentionRow> LoadResolvedDocumentMentionsByDocument(string documentId);

    void ReplaceDocumentMentions(
        string sourceDocumentId,
        IReadOnlyList<DocumentMentionRow> mentions);

    IReadOnlyList<AttachmentRow> LoadAttachments();

    IReadOnlyList<NoteAttachmentRow> LoadNoteAttachments();

    AttachmentRow? GetAttachment(string attachmentId);

    AttachmentRow? FindAttachmentBySha256(string sha256);

    void UpsertAttachment(AttachmentRow attachment);

    void UpsertNoteAttachment(NoteAttachmentRow attachment);

    void RenameItem(long itemId, string title);

    void DeleteItem(long itemId);
}
