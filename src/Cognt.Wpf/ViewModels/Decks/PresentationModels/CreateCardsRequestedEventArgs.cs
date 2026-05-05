namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed class CreateCardsRequestedEventArgs(
    string deckName,
    string content,
    IReadOnlyList<string> tags) : EventArgs
{
    public string DeckName { get; } = string.IsNullOrWhiteSpace(deckName)
        ? throw new ArgumentException("Deck name is required.", nameof(deckName))
        : deckName;

    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public IReadOnlyList<string> Tags { get; } = tags?.ToArray() ?? throw new ArgumentNullException(nameof(tags));
}
