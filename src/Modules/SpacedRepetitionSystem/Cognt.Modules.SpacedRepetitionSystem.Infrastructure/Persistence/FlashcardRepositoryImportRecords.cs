using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed record FlashcardCollectionImportSnapshot(
    IReadOnlyList<FlashcardImportDeckRecord> Decks,
    IReadOnlyList<FlashcardImportCardRecord> Cards,
    IReadOnlyList<FlashcardImportReviewHistoryRecord> ReviewHistory);

internal sealed record FlashcardImportDeckRecord(
    long Id,
    string Name);

internal sealed record FlashcardImportCardRecord(
    long Id,
    long DeckId,
    string CardType,
    string FrontRelPath,
    string? BackRelPath,
    string? SourceRelPath,
    string? SourceContextId,
    string? SourceSlotId,
    string? SourceTargetId,
    string? SourceSnapshotHash,
    int? ClozeIndex,
    string SerializedState,
    DateTimeOffset DueUtc,
    FlashcardLearningState State,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? LastReviewUtc,
    int ElapsedDays,
    int ScheduledDays,
    int Reps,
    int Lapses,
    bool IsSuspended,
    DateTimeOffset? SuspendedAtUtc,
    long CreatedAt,
    long UpdatedAt);

internal sealed record FlashcardImportReviewHistoryRecord(
    long CardId,
    DateTimeOffset ReviewedAtUtc,
    int Rating,
    DateTimeOffset PreviousDueUtc,
    DateTimeOffset NextDueUtc,
    FlashcardLearningState PreviousState,
    FlashcardLearningState NextState,
    string? ReviewLogJson);

internal sealed record PreparedFlashcardImportCard(
    FlashcardImportCardRecord Source,
    string DeckName,
    string FrontRelPath,
    string? BackRelPath,
    string? SourceRelPath,
    string? SourceContextId,
    string? SourceSlotId,
    string? SourceTargetId,
    string? SourceSnapshotHash);
