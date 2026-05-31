using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardCollectionImportSnapshot LoadCollectionImportSnapshot()
    {
        return UseConnection(connection => new FlashcardCollectionImportSnapshot(
            LoadImportDecks(connection),
            LoadImportCards(connection),
            LoadImportReviewHistory(connection)));
    }

    internal long? TryImportCard(PreparedFlashcardImportCard card)
    {
        return UseConnection<long?>(connection =>
        {
            long deckId = EnsureDeck(card.DeckName);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO flashcard_card (
                    uid, deck_id, card_type, front_rel_path, back_rel_path, source_rel_path,
                    source_context_id, source_slot_id, source_target_id, source_snapshot_hash,
                    cloze_index,
                    fsrs_card_state_json, due_at, state, stability, difficulty,
                    fsrs_step, last_review_at, elapsed_days, scheduled_days, reps, lapses,
                    is_suspended, suspended_at, created_at, updated_at
                )
                VALUES (
                    $uid, $deckId, $cardType, $frontRelPath, $backRelPath, $sourceRelPath,
                    $sourceContextId, $sourceSlotId, $sourceTargetId, $sourceSnapshotHash,
                    $clozeIndex,
                    $fsrsCardStateJson, $dueAt, $state, $stability, $difficulty,
                    $fsrsStep, $lastReviewAt, $elapsedDays, $scheduledDays, $reps, $lapses,
                    $isSuspended, $suspendedAt, $createdAt, $updatedAt
                )
                RETURNING id;
                """;
            AddImportedCardParameters(command, deckId, card);

            try
            {
                return Convert.ToInt64(command.ExecuteScalar());
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return null;
            }
        });
    }

    internal int ImportReviewHistory(
        IReadOnlyDictionary<long, long> importedCardIdsBySourceId,
        IReadOnlyList<FlashcardImportReviewHistoryRecord> reviewHistory)
    {
        if (importedCardIdsBySourceId.Count == 0 || reviewHistory.Count == 0)
        {
            return 0;
        }

        return UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            int imported = 0;
            foreach (FlashcardImportReviewHistoryRecord history in reviewHistory)
            {
                if (!importedCardIdsBySourceId.TryGetValue(history.CardId, out long importedCardId))
                {
                    continue;
                }

                InsertImportedReviewHistory(connection, transaction, importedCardId, history);
                imported++;
            }

            transaction.Commit();
            return imported;
        });
    }

    private static IReadOnlyList<FlashcardImportDeckRecord> LoadImportDecks(SqliteConnection connection)
    {
        return QueryRows(
            connection,
            """
            SELECT id, name
            FROM flashcard_deck
            WHERE EXISTS (
                SELECT 1
                FROM flashcard_card c
                WHERE c.deck_id = flashcard_deck.id
                  AND c.is_deleted = 0
            )
            ORDER BY name COLLATE NOCASE, id;
            """,
            static reader => new FlashcardImportDeckRecord(reader.GetInt64(0), reader.GetString(1)));
    }

    private static IReadOnlyList<FlashcardImportCardRecord> LoadImportCards(SqliteConnection connection)
    {
        return QueryRows(
            connection,
            """
            SELECT id, deck_id, card_type, front_rel_path, back_rel_path,
                   source_rel_path, source_context_id, source_slot_id, source_target_id,
                   source_snapshot_hash, cloze_index, fsrs_card_state_json, due_at, state,
                   stability, difficulty, fsrs_step, last_review_at, elapsed_days,
                   scheduled_days, reps, lapses, is_suspended, suspended_at,
                   created_at, updated_at
            FROM flashcard_card
            WHERE is_deleted = 0
            ORDER BY created_at, id;
            """,
            ReadImportCard);
    }

    private static FlashcardImportCardRecord ReadImportCard(SqliteDataReader reader)
    {
        return new FlashcardImportCardRecord(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetInt32(10),
            reader.GetString(11),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(12)),
            (FlashcardLearningState)reader.GetInt32(13),
            reader.IsDBNull(14) ? null : reader.GetDouble(14),
            reader.IsDBNull(15) ? null : reader.GetDouble(15),
            reader.IsDBNull(16) ? null : reader.GetInt32(16),
            reader.IsDBNull(17) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(17)),
            reader.GetInt32(18),
            reader.GetInt32(19),
            reader.GetInt32(20),
            reader.GetInt32(21),
            reader.GetInt32(22) != 0,
            reader.IsDBNull(23) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(23)),
            reader.GetInt64(24),
            reader.GetInt64(25));
    }

    private static IReadOnlyList<FlashcardImportReviewHistoryRecord> LoadImportReviewHistory(SqliteConnection connection)
    {
        return QueryRows(
            connection,
            """
            SELECT h.card_id, h.reviewed_at, h.rating, h.previous_due_at, h.next_due_at,
                   h.previous_state, h.next_state, h.review_log_json
            FROM flashcard_review_history h
            INNER JOIN flashcard_card c ON c.id = h.card_id
            WHERE c.is_deleted = 0
            ORDER BY h.reviewed_at, h.id;
            """,
            static reader => new FlashcardImportReviewHistoryRecord(
                reader.GetInt64(0),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                reader.GetInt32(2),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),
                (FlashcardLearningState)reader.GetInt32(5),
                (FlashcardLearningState)reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
    }

    private static IReadOnlyList<T> QueryRows<T>(
        SqliteConnection connection,
        string commandText,
        Func<SqliteDataReader, T> read)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        List<T> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static void AddImportedCardParameters(
        SqliteCommand command,
        long deckId,
        PreparedFlashcardImportCard card)
    {
        FlashcardImportCardRecord source = card.Source;
        command.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$cardType", NormalizeName(source.CardType));
        command.Parameters.AddWithValue("$frontRelPath", NormalizeRelPath(card.FrontRelPath));
        command.Parameters.AddWithValue("$backRelPath", DbValue(NormalizeOptionalRelPath(card.BackRelPath)));
        command.Parameters.AddWithValue("$sourceRelPath", DbValue(NormalizeOptionalRelPath(card.SourceRelPath)));
        command.Parameters.AddWithValue("$sourceContextId", DbValue(card.SourceContextId));
        command.Parameters.AddWithValue("$sourceSlotId", DbValue(card.SourceSlotId));
        command.Parameters.AddWithValue("$sourceTargetId", DbValue(card.SourceTargetId));
        command.Parameters.AddWithValue("$sourceSnapshotHash", DbValue(card.SourceSnapshotHash));
        command.Parameters.AddWithValue("$clozeIndex", DbValue(source.ClozeIndex));
        command.Parameters.AddWithValue("$fsrsCardStateJson", source.SerializedState);
        command.Parameters.AddWithValue("$dueAt", source.DueUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$state", (int)source.State);
        command.Parameters.AddWithValue("$stability", DbValue(source.Stability));
        command.Parameters.AddWithValue("$difficulty", DbValue(source.Difficulty));
        command.Parameters.AddWithValue("$fsrsStep", DbValue(source.Step));
        command.Parameters.AddWithValue("$lastReviewAt", DbValue(source.LastReviewUtc?.ToUnixTimeSeconds()));
        command.Parameters.AddWithValue("$elapsedDays", Math.Max(0, source.ElapsedDays));
        command.Parameters.AddWithValue("$scheduledDays", Math.Max(0, source.ScheduledDays));
        command.Parameters.AddWithValue("$reps", Math.Max(0, source.Reps));
        command.Parameters.AddWithValue("$lapses", Math.Max(0, source.Lapses));
        command.Parameters.AddWithValue("$isSuspended", source.IsSuspended ? 1 : 0);
        command.Parameters.AddWithValue("$suspendedAt", DbValue(source.SuspendedAtUtc?.ToUnixTimeSeconds()));
        command.Parameters.AddWithValue("$createdAt", source.CreatedAt);
        command.Parameters.AddWithValue("$updatedAt", source.UpdatedAt);
    }

    private static void InsertImportedReviewHistory(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long cardId,
        FlashcardImportReviewHistoryRecord history)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO flashcard_review_history (
                uid, card_id, reviewed_at, rating, previous_due_at, next_due_at,
                previous_state, next_state, review_log_json
            )
            VALUES (
                $uid, $cardId, $reviewedAt, $rating, $previousDueAt, $nextDueAt,
                $previousState, $nextState, $reviewLogJson
            );
            """;
        command.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$reviewedAt", history.ReviewedAtUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$rating", history.Rating);
        command.Parameters.AddWithValue("$previousDueAt", history.PreviousDueUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$nextDueAt", history.NextDueUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$previousState", (int)history.PreviousState);
        command.Parameters.AddWithValue("$nextState", (int)history.NextState);
        command.Parameters.AddWithValue("$reviewLogJson", DbValue(history.ReviewLogJson));
        command.ExecuteNonQuery();
    }
}
