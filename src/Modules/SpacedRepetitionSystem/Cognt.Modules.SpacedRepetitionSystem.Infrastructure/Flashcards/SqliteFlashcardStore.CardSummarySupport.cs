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
        string? tag,
        IReadOnlyList<string>? tags)
    {
        List<FlashcardCardSummaryReadModel> summaries = [];
        List<long> staleCardIds = [];
        string[] normalizedTags = NormalizeTagFilters(tag, tags);
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

    private static string[] NormalizeTagFilters(string? tag, IReadOnlyList<string>? tags)
    {
        return (tags ?? [])
            .Concat(string.IsNullOrWhiteSpace(tag) ? [] : [tag])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeTagFilter)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTagFilter(string tag)
    {
        return string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }

    private static bool MatchesAnyTag(IReadOnlyList<string> tags, IReadOnlyList<string> normalizedTags)
    {
        if (normalizedTags.Count == 0)
        {
            return true;
        }

        return tags
            .Select(NormalizeTagFilter)
            .Any(candidate => normalizedTags.Contains(candidate, StringComparer.OrdinalIgnoreCase));
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
