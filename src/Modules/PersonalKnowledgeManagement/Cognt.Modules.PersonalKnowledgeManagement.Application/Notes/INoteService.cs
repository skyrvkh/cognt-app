using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;

namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

public interface INoteService : IDisposable
{
    void CloseCurrentCollection();

    string ReadNoteFile(string collectionId, string relativePath);

    string? ReadCardContextDrafts(string collectionId, string relativePath);

    KnowledgeOpenedNoteReadModel ReadNoteForOpen(string collectionId, string relativePath);

    IReadOnlyList<KnowledgeTreeItemReadModel> LoadRootItems(string collectionId);

    IReadOnlyList<KnowledgeTreeItemReadModel> LoadChildren(string collectionId, long parentId);

    KnowledgeTreeItemReadModel? GetItem(string collectionId, long itemId);

    IReadOnlyList<KnowledgeTreeItemReadModel> LoadBreadcrumbChain(string collectionId, long itemId);

    KnowledgeTreeItemReadModel? FindNoteByRelativePath(string collectionId, string relativePath);

    long CreateFolder(
        string collectionId,
        long? parentId,
        string title,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    KnowledgeCreatedNoteReadModel CreateNote(
        string collectionId,
        long? parentId,
        string title,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    KnowledgeCreatedNoteReadModel CreateExtractNote(
        string collectionId,
        long? parentId,
        string sourceRelativePath,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    void MoveItem(
        string collectionId,
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId);

    void RenameItem(
        string collectionId,
        long itemId,
        string title);

    IReadOnlyList<string> DeleteItem(
        string collectionId,
        long itemId);

    Task<KnowledgeAttachmentReadModel> ImportAttachmentAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default);

    Task<KnowledgePdfDocumentReadModel> ImportPdfDocumentAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default);

    Task<string> AttachFileToNoteAsync(
        string collectionId,
        string noteRelativePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default);

    Task<KnowledgeAttachmentReadModel> AttachFileReferenceToNoteAsync(
        string collectionId,
        string noteRelativePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken = default);

    string? ResolveImageAttachmentRelativePath(
        string collectionId,
        string noteRelativePath,
        string imageSource);

    void SaveNote(string collectionId, string relativePath, string content);

    void SaveCardContextDrafts(string collectionId, string relativePath, string? content);

    IReadOnlyList<KnowledgeMentionTargetReadModel> ListMentionTargets(
        string collectionId);

    IReadOnlyList<KnowledgeMentionTargetReadModel> SearchMentionTargets(
        string collectionId,
        string query,
        int limit);

    IReadOnlySet<string> SearchNoteCatalog(
        string collectionId,
        string query);

    string ReadMentionPreview(
        string collectionId,
        string relativePath,
        int maxCharacters);

    string ResolveMentionPath(
        string collectionId,
        string relativePath);

    IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(
        string collectionId);

    KnowledgeDocumentLinksReadModel ReadDocumentLinks(
        string collectionId,
        string currentRelativePath,
        string currentContent);

    KnowledgeGraphReadModel ReadDocumentGraph(
        string collectionId,
        string currentRelativePath,
        string currentContent,
        KnowledgeGraphSettingsReadModel settings);

    KnowledgeGraphSettingsReadModel LoadGraphViewSettings(
        string collectionId);

    void SaveGraphViewSettings(
        string collectionId,
        KnowledgeGraphSettingsReadModel settings);
}
