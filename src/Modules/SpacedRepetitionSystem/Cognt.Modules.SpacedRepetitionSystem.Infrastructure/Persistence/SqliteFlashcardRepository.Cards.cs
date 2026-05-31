using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardRecord GetCard(long cardId)
    {
        return UseConnection(connection => GetCard(cardId, connection));
    }

    internal IReadOnlyList<FlashcardRecord> LoadCardsByIds(IReadOnlyList<long> cardIds)
    {
        long[] distinctIds = CreateDistinctPositiveIds(cardIds);
        if (distinctIds.Length == 0)
        {
            return [];
        }

        return UseConnection(connection =>
        {
            List<FlashcardRecord> records = new(distinctIds.Length);
            for (int offset = 0; offset < distinctIds.Length; offset += 400)
            {
                int count = Math.Min(400, distinctIds.Length - offset);
                using SqliteCommand command = CreateLoadCardsByIdsCommand(connection, distinctIds, offset, count);
                records.AddRange(ReadCards(command));
            }

            return records;
        });
    }

}
