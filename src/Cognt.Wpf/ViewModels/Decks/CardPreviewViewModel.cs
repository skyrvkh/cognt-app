using CogntApp.Models.Decks;

namespace CogntApp.ViewModels.Decks;

public sealed class CardPreviewViewModel
{
    public CardPreviewViewModel(CardLibraryRow card)
    {
        ArgumentNullException.ThrowIfNull(card);

        FrontText = NormalizeText(card.Question);
        BackText = "-";
        Metadata = $"{card.Deck} - {card.Status} - {card.Due}";
    }

    public string FrontText { get; }

    public string BackText { get; }

    public string Metadata { get; }

    private static string NormalizeText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? "-" : text;
    }
}
