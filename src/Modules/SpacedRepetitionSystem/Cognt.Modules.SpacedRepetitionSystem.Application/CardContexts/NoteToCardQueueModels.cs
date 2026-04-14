namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed record GeneratedCardContextCardReadModel(
    long CardId,
    string SourceRelPath,
    string ContextId,
    string SlotId,
    string SnapshotHash);

public sealed record TextAnchorPreviewReadModel(
    string TargetId,
    string AnchorId,
    string BlockId,
    string? MarkId);

public sealed record ImageMaskPreviewReadModel(
    string TargetId,
    string AnchorId,
    string ImageBlockId,
    string MaskId,
    string? ImageRelPath,
    string? Label);

public enum CardCandidateStatus
{
    Draft = 0,
    Ready = 1,
    Invalid = 2,
    Generated = 3,
    Outdated = 4,
    Disabled = 5,
}

public sealed record CardCandidateReadModel(
    string ContextId,
    string SlotId,
    int DisplayClozeIndex,
    IReadOnlyList<string> TargetIds,
    string AnswerPreview,
    IReadOnlyList<TextAnchorPreviewReadModel> TextAnchors,
    IReadOnlyList<ImageMaskPreviewReadModel> ImageAnchors,
    CardCandidateStatus Status,
    string? ValidationMessage,
    string SnapshotHash);

public sealed record NoteToCardQueueSummaryReadModel(
    int TotalItems,
    int DraftItems,
    int ReadyItems,
    int InvalidItems,
    int GeneratedItems,
    int OutdatedItems,
    int DisabledItems);

public sealed record NoteToCardQueueReadModel(
    string CollectionId,
    string SourceRelPath,
    IReadOnlyList<CardCandidateReadModel> Items,
    NoteToCardQueueSummaryReadModel Summary);
