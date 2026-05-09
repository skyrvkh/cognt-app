namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed class SaveCardRequestedEventArgs(
    long cardId,
    string content) : EventArgs
{
    public long CardId { get; } = EnsurePersistedCardId(cardId);

    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    private static long EnsurePersistedCardId(long cardId)
    {
        return cardId > 0
            ? cardId
            : throw new ArgumentOutOfRangeException(nameof(cardId), cardId, "A persisted card id is required.");
    }
}
