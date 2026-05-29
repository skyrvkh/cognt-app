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
    long ItemId,
    string Title,
    string RelativePath,
    KnowledgeNoteListItemType Type,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string>? AssignedTags = null,
    IReadOnlyList<string>? ContentTags = null);

public enum KnowledgeTagOperationKind
{
    Add = 1,
    Remove = 2
}

public sealed record KnowledgeTagOperationResult(
    int RequestedRootCount,
    int AffectedItemCount,
    IReadOnlyList<string> Tags,
    bool IncludedDescendants,
    KnowledgeTagOperationKind Operation);

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

public sealed record KnowledgeCollectionImportReadModel(
    string SourceCollectionPath,
    string ImportFolderTitle,
    int ImportedFolders,
    int ImportedNotes,
    int ImportedPdfDocuments,
    int ImportedAttachments,
    int ImportedAttachmentLinks,
    int ImportedTaggedItems,
    int SkippedMissingFiles,
    IReadOnlyDictionary<string, string> ImportedDocumentPathMap);

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

public sealed record KnowledgeGraphSettingsReadModel(
    int NeighborDepth,
    int MaxNodes)
{
    public const int DefaultNeighborDepth = 1;
    public const int MinimumNeighborDepth = 1;
    public const int MaximumNeighborDepth = 4;
    public const int DefaultMaxNodes = 75;
    public const int MinimumMaxNodes = 10;
    public const int MaximumMaxNodes = 300;

    public static KnowledgeGraphSettingsReadModel Default { get; } = new(
        DefaultNeighborDepth,
        DefaultMaxNodes);

    public KnowledgeGraphSettingsReadModel Normalize()
    {
        return this with
        {
            NeighborDepth = Math.Clamp(NeighborDepth, MinimumNeighborDepth, MaximumNeighborDepth),
            MaxNodes = Math.Clamp(MaxNodes, MinimumMaxNodes, MaximumMaxNodes),
        };
    }
}

public sealed record KnowledgeGraphReadModel(
    string? CurrentDocumentId,
    string? CurrentRelativePath,
    KnowledgeGraphSettingsReadModel Settings,
    IReadOnlyList<KnowledgeGraphNodeReadModel> Nodes,
    IReadOnlyList<KnowledgeGraphEdgeReadModel> Edges,
    int TotalReachableNodeCount,
    bool IsTruncated)
{
    public static KnowledgeGraphReadModel Empty(KnowledgeGraphSettingsReadModel settings)
    {
        return new KnowledgeGraphReadModel(
            CurrentDocumentId: null,
            CurrentRelativePath: null,
            settings.Normalize(),
            Nodes: [],
            Edges: [],
            TotalReachableNodeCount: 0,
            IsTruncated: false);
    }
}

public sealed record KnowledgeGraphNodeReadModel(
    string DocumentId,
    string RelativePath,
    string Title,
    int Depth,
    bool IsCurrent,
    long? UpdatedAt,
    int IncomingEdgeCount,
    int OutgoingEdgeCount);

public sealed record KnowledgeGraphEdgeReadModel(
    string SourceDocumentId,
    string TargetDocumentId,
    int MentionCount,
    long? UpdatedAt);
