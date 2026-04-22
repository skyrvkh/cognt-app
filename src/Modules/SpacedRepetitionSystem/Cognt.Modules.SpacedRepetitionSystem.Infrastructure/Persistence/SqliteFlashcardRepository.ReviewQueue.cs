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
}
