namespace CogntApp.Models.Tree;

public enum TreeItemKind
{
    Folder = 1,
    Note = 2,
    Root = 3,
    Trash = 4,
}

public sealed record TreeItemRow(
    long Id,
    string Uid,
    long? ParentId,
    TreeItemKind Kind,
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
