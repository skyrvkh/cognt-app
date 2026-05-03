using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void UpdateGeneratedCard(
        long cardId,
        long deckId,
        string cardType,
        string? sourceRelPath,
        string? sourceContextId,
        string? sourceSlotId,
        string? sourceTargetId,
        string? sourceSnapshotHash)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE flashcard_card
                SET deck_id = $deckId,
                    card_type = $cardType,
                    source_rel_path = $sourceRelPath,
                    source_context_id = $sourceContextId,
                    source_slot_id = $sourceSlotId,
                    source_target_id = $sourceTargetId,
                    source_snapshot_hash = $sourceSnapshotHash,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", cardId);
            command.Parameters.AddWithValue("$deckId", deckId);
            command.Parameters.AddWithValue("$cardType", NormalizeName(cardType));
            command.Parameters.AddWithValue("$sourceRelPath", DbValue(NormalizeOptionalRelPath(sourceRelPath)));
            command.Parameters.AddWithValue("$sourceContextId", DbValue(sourceContextId));
            command.Parameters.AddWithValue("$sourceSlotId", DbValue(sourceSlotId));
            command.Parameters.AddWithValue("$sourceTargetId", DbValue(sourceTargetId));
            command.Parameters.AddWithValue("$sourceSnapshotHash", DbValue(sourceSnapshotHash));
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
            command.ExecuteNonQuery();
        });
    }
}
