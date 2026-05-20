using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FlashcardReviewQueueCandidate> LoadReviewQueueCandidates(
        DateTimeOffset nowUtc,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        int learningLimit,
        int relearningLimit,
        int reviewLimit,
        int newLimit)
    {
        ReviewQueueQueryBounds bounds = new(
            nowUtc.ToUnixTimeSeconds(),
            todayStartUtc.ToUnixTimeSeconds(),
            tomorrowStartUtc.ToUnixTimeSeconds());

        return UseConnection(connection =>
        {
            List<FlashcardReviewQueueCandidate> candidates = [];
            candidates.AddRange(LoadCandidatesByKind(
                connection,
                FlashcardReviewQueueCategory.Learning,
                $"""
                SELECT id, due_at
                FROM flashcard_card
                WHERE {ActiveSingleFileCardPredicate}
                  AND state = {LearningStateValue}
                  AND due_at <= $now
                ORDER BY due_at, id
                LIMIT $limit;
                """,
                bounds,
                learningLimit));
            candidates.AddRange(LoadCandidatesByKind(
                connection,
                FlashcardReviewQueueCategory.Relearning,
                $"""
                SELECT id, due_at
                FROM flashcard_card
                WHERE {ActiveSingleFileCardPredicate}
                  AND state = {RelearningStateValue}
                  AND due_at <= $now
                ORDER BY due_at, id
                LIMIT $limit;
                """,
                bounds,
                relearningLimit));
            candidates.AddRange(LoadCandidatesByKind(
                connection,
                FlashcardReviewQueueCategory.Overdue,
                $"""
                SELECT id, due_at
                FROM flashcard_card
                WHERE {ActiveSingleFileCardPredicate}
                  AND state = {ReviewStateValue}
                  AND due_at < $todayStart
                ORDER BY due_at, id
                LIMIT $limit;
                """,
                bounds,
                reviewLimit));
            candidates.AddRange(LoadCandidatesByKind(
                connection,
                FlashcardReviewQueueCategory.Due,
                $"""
                SELECT id, due_at
                FROM flashcard_card
                WHERE {ActiveSingleFileCardPredicate}
                  AND state = {ReviewStateValue}
                  AND due_at >= $todayStart
                  AND due_at < $tomorrowStart
                  AND due_at <= $now
                ORDER BY due_at, id
                LIMIT $limit;
                """,
                bounds,
                reviewLimit));
            candidates.AddRange(LoadCandidatesByKind(
                connection,
                FlashcardReviewQueueCategory.New,
                $"""
                SELECT id, due_at
                FROM flashcard_card
                WHERE {ActiveSingleFileCardPredicate}
                  AND state = {NewStateValue}
                ORDER BY created_at, id
                LIMIT $limit;
                """,
                bounds,
                newLimit));

            return candidates;
        });
    }

    internal IReadOnlyList<FlashcardReviewQueueCandidate> LoadCramReviewQueueCandidates(
        DateTimeOffset nowUtc,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        CramScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        long[] deckIds = NormalizeDeckScope(scope.DeckIds);
        string deckFilter = CreateDeckFilterClause(deckIds);
        string suspendedFilter = scope.ExcludeSuspendedCards
            ? "AND is_suspended = 0"
            : string.Empty;
        ReviewQueueQueryBounds bounds = new(
            nowUtc.ToUnixTimeSeconds(),
            todayStartUtc.ToUnixTimeSeconds(),
            tomorrowStartUtc.ToUnixTimeSeconds());

        return UseConnection(connection => LoadCandidatesByDynamicKind(
            connection,
            $"""
            SELECT
                id,
                due_at,
                CASE
                    WHEN state = {LearningStateValue} THEN {(int)FlashcardReviewQueueCategory.Learning}
                    WHEN state = {RelearningStateValue} THEN {(int)FlashcardReviewQueueCategory.Relearning}
                    WHEN state = {ReviewStateValue} AND due_at < $todayStart THEN {(int)FlashcardReviewQueueCategory.Overdue}
                    WHEN state = {ReviewStateValue} THEN {(int)FlashcardReviewQueueCategory.Due}
                    ELSE {(int)FlashcardReviewQueueCategory.New}
                END AS queue_category
            FROM flashcard_card
            WHERE is_deleted = 0
              AND back_rel_path IS NULL
              {suspendedFilter}
              {deckFilter}
              AND state IN ({NewStateValue}, {LearningStateValue}, {ReviewStateValue}, {RelearningStateValue})
            ORDER BY queue_category,
                     CASE WHEN state = {NewStateValue} THEN created_at ELSE due_at END,
                     id;
            """,
            bounds,
            command => AddDeckFilterParameters(command, deckIds)));
    }
}
