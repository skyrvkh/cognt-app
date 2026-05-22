using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class CardsPresentationFactory
{
    internal const string AllSourcesValue = "__all_sources";
    internal const string AllTagsValue = "__all_tags";

    internal static IReadOnlyList<CardLibraryRow> CreateCardLibraryRows(
        IReadOnlyList<FlashcardCardSummaryReadModel> visibleCards)
    {
        return visibleCards
            .Select((card, index) => ToCardLibraryRow(card, index == 0))
            .ToArray();
    }

    private static CardLibraryRow ToCardLibraryRow(FlashcardCardSummaryReadModel card, bool isSelected)
    {
        return CreateCardLibraryRow(
            isSelected,
            card.Id,
            card.FrontText,
            card.BackText,
            card.DeckName,
            card.DueUtc,
            card.State,
            card.LastReviewedUtc,
            card.LastRating,
            card.FrontContent,
            card.Tags,
            card.IsSuspended);
    }

    private static CardLibraryRow CreateCardLibraryRow(
        bool isSelected,
        long id,
        string frontText,
        string backText,
        string deckName,
        DateTimeOffset dueUtc,
        FlashcardCardStateReadModel state,
        DateTimeOffset? lastReviewedUtc,
        FlashcardReviewRatingReadModel? lastRating,
        string frontContent,
        IReadOnlyList<string> tags,
        bool isSuspended)
    {
        string due = FormatDue(dueUtc);
        string lastReviewed = FormatLastReviewed(lastReviewedUtc);
        string recall = FormatRecall(lastRating);

        CardLibraryRow row = isSuspended
            ? new CardLibraryRow(frontText, deckName, "Suspended", due, lastReviewed, recall, isSelected)
            : state switch
        {
            FlashcardCardStateReadModel.Relearning => CardLibraryRow.Relearning(isSelected, frontText, deckName, due, lastReviewed, recall),
            FlashcardCardStateReadModel.Review => CardLibraryRow.Review(isSelected, frontText, deckName, due, lastReviewed, recall),
            FlashcardCardStateReadModel.Learning => CardLibraryRow.Learning(isSelected, frontText, deckName, due, lastReviewed, recall),
            _ => CardLibraryRow.Learning(isSelected, frontText, deckName, due, lastReviewed, recall)
        };

        return row with
        {
            Id = id,
            Answer = backText,
            FrontContent = frontContent,
            DueUtc = dueUtc,
            Tags = tags,
            IsSuspended = isSuspended
        };
    }

    private static string FormatDue(DateTimeOffset due)
    {
        DateOnly localDate = ToLocalDate(due);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        if (localDate <= today)
        {
            return "Today";
        }

        if (localDate == today.AddDays(1))
        {
            return "Tomorrow";
        }

        return localDate.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatLastReviewed(DateTimeOffset? value)
    {
        return value.HasValue ? value.Value.LocalDateTime.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture) : "Never";
    }

    private static string FormatRecall(FlashcardReviewRatingReadModel? rating)
    {
        return rating?.ToString() ?? "-";
    }
}
