using System.Text;
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
            materialized.BackKind);
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
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedTags = [];
        AddNormalizedTags(tags, seenTags, normalizedTags);
        AddNormalizedTag(tag, seenTags, normalizedTags);
        return normalizedTags.ToArray();
    }

    private static void AddNormalizedTags(
        IEnumerable<string>? tags,
        HashSet<string> seenTags,
        List<string> normalizedTags)
    {
        if (tags is null)
        {
            return;
        }

        foreach (string tag in tags)
        {
            AddNormalizedTag(tag, seenTags, normalizedTags);
        }
    }

    private static void AddNormalizedTag(
        string? tag,
        HashSet<string> seenTags,
        List<string> normalizedTags)
    {
        string normalizedTag = NormalizeTagFilter(tag);
        if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
        {
            normalizedTags.Add(normalizedTag);
        }
    }

    private static string NormalizeTagFilter(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedTag = tag.AsSpan().Trim();
        int firstWhitespaceIndex = IndexOfWhitespace(trimmedTag);
        if (firstWhitespaceIndex < 0)
        {
            return trimmedTag.ToString();
        }

        StringBuilder builder = new(trimmedTag.Length);
        builder.Append(trimmedTag[..firstWhitespaceIndex]);
        for (int index = firstWhitespaceIndex + 1; index < trimmedTag.Length; index++)
        {
            char character = trimmedTag[index];
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static int IndexOfWhitespace(ReadOnlySpan<char> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool MatchesAnyTag(IReadOnlyList<string> tags, IReadOnlyList<string> normalizedTags)
    {
        if (normalizedTags.Count == 0)
        {
            return true;
        }

        foreach (string tag in tags)
        {
            string normalizedTag = NormalizeTagFilter(tag);
            if (ContainsTag(normalizedTags, normalizedTag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTag(IReadOnlyList<string> tags, string candidate)
    {
        if (candidate.Length == 0)
        {
            return false;
        }

        foreach (string tag in tags)
        {
            if (string.Equals(tag, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeSearchText(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();
    }

    private static bool MatchesSearch(
        FlashcardCardSummaryRecord card,
        MaterializedFlashcardContent materialized,
        string searchText)
    {
        if (searchText.Length == 0)
        {
            return true;
        }

        return ContainsSearchText(materialized.FrontText, searchText)
            || ContainsSearchText(materialized.BackText, searchText)
            || ContainsSearchText(card.DeckName, searchText)
            || ContainsSearchText(card.SourceRelPath, searchText)
            || ContainsTagSearchText(materialized.Tags, searchText);
    }

    private static bool ContainsTagSearchText(IReadOnlyList<string> tags, string searchText)
    {
        foreach (string tag in tags)
        {
            if (ContainsSearchText(tag, searchText))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSearchText(string? value, string searchText)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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
