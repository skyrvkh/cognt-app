using CogntApp.Models.Decks;

namespace CogntApp.ViewModels.Decks;

public sealed class DecksViewModel
{
    public IReadOnlyList<DeckRow> DeckRows { get; } =
    [
        new(true, "Biology 101", "Biology", 4, 8, 11, 152, 68, "#EFF6FF", "#B8D3FF", "#2563EB", "#EEF2FF"),
        new(false, "Anatomy Terms", "Anatomy", 6, 5, 9, 128, 54, "#FAF5FF", "#E4C6FF", "#9333EA", "#FFFFFF"),
        new(false, "Chemistry Review", "Chemistry", 3, 7, 7, 110, 47, "#ECFDF3", "#B7E4CA", "#16A34A", "#FFFFFF"),
        new(false, "Physics Formulas", "Physics", 5, 4, 6, 96, 61, "#EFF6FF", "#B8D3FF", "#2563EB", "#FFFFFF"),
        new(false, "Spanish Vocabulary", "Languages", 8, 6, 10, 143, 49, "#EFF6FF", "#B8D3FF", "#2563EB", "#FFFFFF"),
        new(false, "World History", "History", 2, 3, 5, 89, 33, "#EFF6FF", "#B8D3FF", "#2563EB", "#FFFFFF")
    ];

    public IReadOnlyList<CardRow> CardRows { get; } =
    [
        CardRow.Mastered("What is the powerhouse of the cell?", "May 20, 2025"),
        CardRow.Learning("What are the four main types of tissue in animals?", "May 19, 2025"),
        CardRow.Due("Define osmosis.", "May 18, 2025"),
        CardRow.New("What is the function of ribosomes?", "Never"),
        CardRow.Mastered("What is the structure of DNA?", "May 17, 2025")
    ];
}
