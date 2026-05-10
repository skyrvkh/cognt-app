using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed class CardPreviewViewModel
{
    public CardPreviewViewModel(CardLibraryRow card)
    {
        ArgumentNullException.ThrowIfNull(card);

        FrontText = NormalizeText(card.Question);
        BackText = NormalizeText(card.Answer);
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
