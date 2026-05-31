using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class CardSummaryMaterializer
{
    internal static CardSummaries Load(
        string root,
        IEnumerable<FlashcardCardSummaryRecord> cards,
        string? tag,
        IReadOnlyList<string>? tags,
        string? searchText)
    {
        List<FlashcardCardSummaryReadModel> summaries = [];
        List<long> staleCardIds = [];
        string[] normalizedTags = NormalizeTagFilters(tag, tags);
        string normalizedSearchText = NormalizeSearchText(searchText);
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

            if (!MatchesAnyTag(materialized.Tags, normalizedTags))
            {
                continue;
            }

            if (!MatchesSearch(card, materialized, normalizedSearchText))
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
            card.IsSuspended,
            materialized.Status,
            materialized.BackContent,
            materialized.CardType,
            materialized.FrontKind,
            materialized.BackKind,
            materialized.HasImageOcclusion);
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

}

internal sealed class CardSummaries
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
