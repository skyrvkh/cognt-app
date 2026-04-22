using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public sealed record FlashcardDeckSummaryReadModel(
    long Id,
    string Name,
    int NewCards,
    int LearningCards,
    int DueCards,
    int TotalCards)
{
    public int ProgressPercent
    {
        get
        {
            if (TotalCards <= 0)
            {
                return 0;
            }

            int reviewedOrNotDueCards = Math.Max(0, TotalCards - DueCards);
            int progress = (int)Math.Round(100d * reviewedOrNotDueCards / TotalCards);
            return Math.Clamp(progress, 0, 100);
        }
    }
}

public sealed record FlashcardCardSummaryReadModel(
    long Id,
    string DeckName,
    string? SourceRelPath,
    string FrontText,
    string BackText,
    string FrontContent,
    IReadOnlyList<string> Tags,
    DateTimeOffset DueUtc,
    FlashcardCardStateReadModel State,
    FlashcardReviewRatingReadModel? LastRating,
    DateTimeOffset? LastReviewedUtc,
    FlashcardMaterializationStatus MaterializationStatus = FlashcardMaterializationStatus.Ok);
