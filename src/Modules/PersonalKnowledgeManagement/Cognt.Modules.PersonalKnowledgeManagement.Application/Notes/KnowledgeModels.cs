namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

public enum KnowledgeItemKind
{
    Folder = 1,
    Note = 2,
    Root = 3,
    Trash = 4,
    PdfDocument = 5
}

public sealed record KnowledgeTreeItemReadModel(
    long Id,
    long? ParentId,
    KnowledgeItemKind Kind,
    string Title,
    string? FileRelPath,
    int ChildCount);

public enum KnowledgeNoteListItemType
{
    Note = 1,
    Markdown = 2,
    PdfLinkedNote = 3,
    AttachmentBackedNote = 4
}

public sealed record KnowledgeNoteListItemReadModel(
    string Title,
    string RelativePath,
    KnowledgeNoteListItemType Type,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record KnowledgeOpenedNoteReadModel(
    string RelativePath,
    string Title,
    string Content,
    long? ItemId);

public sealed record KnowledgeCreatedNoteReadModel(
    long ItemId,
    long? ParentId,
    string RelativePath,
    string Title);

public sealed record KnowledgeMentionTargetReadModel(
    string RelativePath,
    string Title);

public sealed record KnowledgePdfDocumentReadModel(
    string RelativePath,
    string Title);

public sealed record KnowledgeDocumentLinksReadModel(
    IReadOnlyList<KnowledgeDocumentLinkReadModel> Incoming,
    IReadOnlyList<KnowledgeDocumentLinkReadModel> Outgoing);

public sealed record KnowledgeDocumentLinkReadModel(
    string OpenRelativePath,
    string DisplayTitle,
    string Snippet,
    string MatchedText,
    long? UpdatedAt);
