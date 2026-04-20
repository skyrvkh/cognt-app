using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal long CreateCard(
        long deckId,
        string cardType,
        string frontRelPath,
        string? sourceRelPath,
        string? sourceContextId,
        string? sourceSlotId,
        string? sourceTargetId,
        string? sourceSnapshotHash,
        int? clozeIndex,
        FlashcardSchedule initialSchedule)
    {
        return UseConnection(connection =>
        {
            long now = NowUnixTime();
            return InsertCard(
                connection,
                deckId,
                cardType,
                frontRelPath,
                sourceRelPath,
                sourceContextId,
                sourceSlotId,
                sourceTargetId,
                sourceSnapshotHash,
                clozeIndex,
                initialSchedule,
                now);
        });
    }

    private static long InsertCard(
        SqliteConnection connection,
        long deckId,
        string cardType,
        string frontRelPath,
        string? sourceRelPath,
        string? sourceContextId,
        string? sourceSlotId,
        string? sourceTargetId,
        string? sourceSnapshotHash,
        int? clozeIndex,
        FlashcardSchedule initialSchedule,
        long now)
    {
        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO flashcard_card (
                uid, deck_id, card_type, front_rel_path, source_rel_path,
                source_context_id, source_slot_id, source_target_id, source_snapshot_hash,
                cloze_index,
                fsrs_card_state_json, due_at, state, stability, difficulty,
                fsrs_step, last_review_at, elapsed_days, scheduled_days, reps, lapses,
                created_at, updated_at
            )
            VALUES (
                $uid, $deckId, $cardType, $frontRelPath, $sourceRelPath,
                $sourceContextId, $sourceSlotId, $sourceTargetId, $sourceSnapshotHash,
                $clozeIndex,
                $fsrsCardStateJson, $dueAt, $state, $stability, $difficulty,
                $fsrsStep, $lastReviewAt, $elapsedDays, $scheduledDays, $reps, $lapses,
                $createdAt, $updatedAt
            )
            RETURNING id;
            """;
        insertCommand.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        insertCommand.Parameters.AddWithValue("$deckId", deckId);
        insertCommand.Parameters.AddWithValue("$cardType", NormalizeName(cardType));
        insertCommand.Parameters.AddWithValue("$frontRelPath", NormalizeRelPath(frontRelPath));
        insertCommand.Parameters.AddWithValue("$sourceRelPath", DbValue(NormalizeOptionalRelPath(sourceRelPath)));
        insertCommand.Parameters.AddWithValue("$sourceContextId", DbValue(sourceContextId));
        insertCommand.Parameters.AddWithValue("$sourceSlotId", DbValue(sourceSlotId));
        insertCommand.Parameters.AddWithValue("$sourceTargetId", DbValue(sourceTargetId));
        insertCommand.Parameters.AddWithValue("$sourceSnapshotHash", DbValue(sourceSnapshotHash));
        insertCommand.Parameters.AddWithValue("$clozeIndex", DbValue(clozeIndex));
        AddScheduleParameters(insertCommand, initialSchedule);
        insertCommand.Parameters.AddWithValue("$createdAt", now);
        insertCommand.Parameters.AddWithValue("$updatedAt", now);

        return Convert.ToInt64(insertCommand.ExecuteScalar());
    }
}
