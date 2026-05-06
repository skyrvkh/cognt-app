using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private static CardSummaries LoadMaterializedCardSummaries(
        string root,
        IEnumerable<FlashcardCardSummaryRecord> cards,
        string? tag)
    {
        List<FlashcardCardSummaryReadModel> summaries = [];
        List<long> staleCardIds = [];
        string? normalizedTag = NormalizeTagFilter(tag);
        foreach (FlashcardCardSummaryRecord card in cards)
        {
            if (!FlashcardCollectionStorage.TryLoadMaterializedCard(
                    root,
                    card.Id,
                    card.FrontRelPath,
                    staleCardIds,
                    out MaterializedFlashcardContent materialized))
            {
                continue;
            }

            if (!MatchesTag(materialized.Tags, normalizedTag))
            {
                continue;
            }

            summaries.Add(ToCardSummaryReadModel(card, materialized));
        }

        return CardSummaries.Create(summaries, staleCardIds);
    }

    private static FlashcardCardSummaryReadModel ToCardSummaryReadModel(
        FlashcardCardSummaryRecord card,
        MaterializedFlashcardContent materialized)
    {
        return new FlashcardCardSummaryReadModel(
            card.Id,
            card.DeckName,
            card.SourceRelPath,
            materialized.FrontText,
            materialized.BackText,
            materialized.EditableContent,
            materialized.Tags,
            card.DueUtc,
            ToCardState(card.State),
            ToReviewRating(card.LastRating),
            card.LastReviewedUtc,
            materialized.Status);
    }

    private static FlashcardCardStateReadModel ToCardState(FlashcardLearningState state)
    {
        return (FlashcardCardStateReadModel)(int)state;
    }

    private static Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardReviewRatingReadModel? ToReviewRating(
        FlashcardReviewRating? rating)
    {
        return rating is null
            ? null
            : (Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardReviewRatingReadModel)(int)rating.Value;
    }

    private static string? NormalizeTagFilter(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
    }

    private static bool MatchesTag(IReadOnlyList<string> tags, string? normalizedTag)
    {
        if (normalizedTag is null)
        {
            return true;
        }

        return tags.Any(candidate => string.Equals(
            candidate.Trim(),
            normalizedTag,
            StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CardSummaries
    {
        internal CardSummaries(
            IReadOnlyList<FlashcardCardSummaryReadModel> summaries,
            IReadOnlyList<long> staleCardIds)
        {
            Summaries = summaries;
            StaleCardIds = staleCardIds;
        }

        internal IReadOnlyList<FlashcardCardSummaryReadModel> Summaries { get; }

        internal IReadOnlyList<long> StaleCardIds { get; }

        internal static CardSummaries Create(
            IReadOnlyList<FlashcardCardSummaryReadModel> summaries,
            IReadOnlyList<long> staleCardIds)
        {
            return new CardSummaries(summaries, staleCardIds);
        }
    }
}
