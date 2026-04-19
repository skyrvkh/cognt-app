using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed class FlashcardRecord
{
    internal FlashcardRecord(
        long id,
        string frontRelPath,
        string serializedState,
        DateTimeOffset dueUtc,
        FlashcardLearningState state)
    {
        Id = id;
        FrontRelPath = frontRelPath;
        SerializedState = serializedState;
        DueUtc = dueUtc;
        State = state;
    }

    internal long Id { get; }

    internal string FrontRelPath { get; }

    internal string SerializedState { get; }

    internal DateTimeOffset DueUtc { get; }

    internal FlashcardLearningState State { get; }
}

internal sealed class GeneratedFlashcardSourceRecord
{
    internal GeneratedFlashcardSourceRecord(
        long id,
        string sourceRelPath,
        long deckId,
        string frontRelPath,
        string? sourceContextId,
        string? sourceSlotId,
        string? sourceSnapshotHash)
    {
        Id = id;
        SourceRelPath = sourceRelPath;
        DeckId = deckId;
        FrontRelPath = frontRelPath;
        SourceContextId = sourceContextId;
        SourceSlotId = sourceSlotId;
        SourceSnapshotHash = sourceSnapshotHash;
    }

    internal long Id { get; }

    internal string SourceRelPath { get; }

    internal long DeckId { get; }

    internal string FrontRelPath { get; }

    internal string? SourceContextId { get; }

    internal string? SourceSlotId { get; }

    internal string? SourceSnapshotHash { get; }
}
