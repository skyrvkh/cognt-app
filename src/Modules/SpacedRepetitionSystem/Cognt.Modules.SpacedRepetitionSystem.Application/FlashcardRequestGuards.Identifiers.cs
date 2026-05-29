namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    private static void EnsureCollectionId(string collectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
    }

    private static void EnsureCardCommandTarget(string collectionId, long cardId)
    {
        EnsureCollectionId(collectionId);
        EnsurePersistedCardId(cardId);
    }

    private static void EnsurePersistedCardId(long cardId)
    {
        if (cardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId), cardId, "A persisted card id is required.");
        }
    }
}
