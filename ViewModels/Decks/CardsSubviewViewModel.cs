using CogntApp.Models.Decks;

namespace CogntApp.ViewModels.Decks;

public sealed class CardsSubviewViewModel
{
    public IReadOnlyList<CardLibraryRow> CardLibraryRows { get; } =
    [
        CardLibraryRow.Mastered(true, "What is the powerhouse of the cell?", "Intro to Biology", "-", "May 20, 2025", "Good"),
        CardLibraryRow.DueCard(false, "Define osmosis.", "Intro to Biology", "Today", "May 18, 2025", "Hard"),
        CardLibraryRow.Learning(false, "What is Newton's second law?", "Physics Formulas", "Tomorrow", "May 21, 2025", "Good"),
        CardLibraryRow.New(false, "Spanish: to eat", "Spanish Vocabulary", "May 23, 2025", "Never", "-"),
        CardLibraryRow.DueCard(false, "Causes of World War I", "World History", "May 24, 2025", "May 16, 2025", "Hard"),
        CardLibraryRow.Learning(false, "What is the chemical symbol for water?", "Chemistry Review", "May 24, 2025", "May 19, 2025", "Good"),
        CardLibraryRow.Mastered(false, "What are the four main types of tissue?", "Intro to Biology", "-", "May 15, 2025", "Easy")
    ];

    public IReadOnlyList<CardFilterDeckRow> CardFilterDeckRows { get; } =
    [
        new("All Decks", "1,248"),
        new("Intro to Biology", "152"),
        new("Chemistry Review", "110"),
        new("Physics Formulas", "96"),
        new("Spanish Vocabulary", "143"),
        new("World History", "89")
    ];
}
