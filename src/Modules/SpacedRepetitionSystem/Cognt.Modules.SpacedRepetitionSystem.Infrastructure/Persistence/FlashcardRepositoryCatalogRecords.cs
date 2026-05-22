using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed class FlashcardDeckSummaryRecord
{
    internal FlashcardDeckSummaryRecord(
        long id,
        string name,
        int newCards,
        int learningCards,
        int dueCards,
        int totalCards)
    {
        Id = id;
        Name = name;
        NewCards = newCards;
        LearningCards = learningCards;
        DueCards = dueCards;
        TotalCards = totalCards;
    }

    internal long Id { get; }

    internal string Name { get; }

    internal int NewCards { get; }

    internal int LearningCards { get; }

    internal int DueCards { get; }

    internal int TotalCards { get; }
}

internal sealed class FlashcardFileReferenceRecord
{
    internal FlashcardFileReferenceRecord(long id, string frontRelPath)
    {
        Id = id;
        FrontRelPath = frontRelPath;
    }

    internal long Id { get; }

    internal string FrontRelPath { get; }
}

internal sealed class FlashcardCardSummaryRecord
{
    internal FlashcardCardSummaryRecord(
        long id,
        string deckName,
        string frontRelPath,
        string? sourceRelPath,
        DateTimeOffset dueUtc,
        FlashcardLearningState state,
        FlashcardReviewRating? lastRating,
        DateTimeOffset? lastReviewedUtc,
        bool isSuspended)
    {
        Id = id;
        DeckName = deckName;
        FrontRelPath = frontRelPath;
        SourceRelPath = sourceRelPath;
        DueUtc = dueUtc;
        State = state;
        LastRating = lastRating;
        LastReviewedUtc = lastReviewedUtc;
        IsSuspended = isSuspended;
    }

    internal long Id { get; }

    internal string DeckName { get; }

    internal string FrontRelPath { get; }

    internal string? SourceRelPath { get; }

    internal DateTimeOffset DueUtc { get; }

    internal FlashcardLearningState State { get; }

    internal FlashcardReviewRating? LastRating { get; }

    internal DateTimeOffset? LastReviewedUtc { get; }

    internal bool IsSuspended { get; }
}
