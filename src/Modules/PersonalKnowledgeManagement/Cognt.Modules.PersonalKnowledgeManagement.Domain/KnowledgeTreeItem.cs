namespace Cognt.Modules.PersonalKnowledgeManagement.Domain;

public enum KnowledgeTreeItemKind
{
    Folder = 1,
    Note = 2,
    Root = 3,
    Trash = 4,
    PdfDocument = 5
}

public sealed record KnowledgeTreeItemRow(
    long Id,
    string Uid,
    long? ParentId,
    KnowledgeTreeItemKind Kind,
    string Title,
    long Position,
    string? FileRelPath,
    int ChildCount,
    bool IsDeleted,
    long CreatedAt,
    long UpdatedAt,
    long? FileSize,
    long? FileMtimeUtc,
    string? ContentHash);
