using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private long AllocatePosition(
        long? parentId,
        long? previousSiblingId,
        long? nextSiblingId,
        SqliteConnection connection,
        SqliteTransaction transaction,
        long? excludedItemId = null,
        bool retried = false)
    {
        long? previousPosition = previousSiblingId.HasValue && previousSiblingId != excludedItemId
            ? GetSiblingPosition(previousSiblingId.Value, parentId, connection, transaction)
            : null;
        long? nextPosition = nextSiblingId.HasValue && nextSiblingId != excludedItemId
            ? GetSiblingPosition(nextSiblingId.Value, parentId, connection, transaction)
            : null;

        if (previousPosition.HasValue && nextPosition.HasValue)
        {
            if (nextPosition.Value > previousPosition.Value
                && nextPosition.Value - previousPosition.Value > 1)
            {
                return previousPosition.Value + ((nextPosition.Value - previousPosition.Value) / 2);
            }

            if (retried)
            {
                throw new InvalidOperationException("Could not allocate a tree position between the selected siblings.");
            }

            RenumberSiblings(parentId, connection, transaction);
            return AllocatePosition(
                parentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId,
                retried: true);
        }

        if (previousPosition.HasValue)
        {
            return previousPosition.Value + PositionGap;
        }

        if (nextPosition.HasValue)
        {
            if (nextPosition.Value > 1)
            {
                return nextPosition.Value / 2;
            }

            if (retried)
            {
                throw new InvalidOperationException("Could not allocate a tree position before the selected sibling.");
            }

            RenumberSiblings(parentId, connection, transaction);
            return AllocatePosition(
                parentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId,
                retried: true);
        }

        long? maxPosition = GetMaxSiblingPosition(parentId, connection, transaction, excludedItemId);
        return maxPosition.HasValue ? maxPosition.Value + PositionGap : PositionGap;
    }
}
