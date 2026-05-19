using System.Globalization;
using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Decks.PresentationModels;

public sealed class DeckRow : ObservableObject
{
    private bool isSelected;

    internal DeckRow(
        long id,
        string name,
        string category,
        int learningQueueCards,
        int dueCards,
        int totalCards,
        int progress,
        bool isSelected)
    {
        Id = id;
        Name = name;
        Category = category;
        LearningQueueCards = learningQueueCards;
        DueCards = dueCards;
        TotalCards = totalCards;
        Progress = progress;
        this.isSelected = isSelected;
    }

    public long Id { get; }

    public string Name { get; }

    public string Category { get; }

    public int LearningQueueCards { get; }

    public int DueCards { get; }

    public int TotalCards { get; }

    public int Progress { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string ProgressLabel => string.Concat(Progress.ToString(CultureInfo.InvariantCulture), "%");
}

public sealed record CardRow(
    string Question,
    string Status,
    string LastReviewed)
{
    internal static CardRow Learning(string question, string lastReviewed)
    {
        return new(question, "Learning", lastReviewed);
    }

    internal static CardRow Relearning(string question, string lastReviewed)
    {
        return new(question, "Relearning", lastReviewed);
    }

    internal static CardRow Review(string question, string lastReviewed)
    {
        return new(question, "Review", lastReviewed);
    }
}

public sealed record CardLibraryRow(
    string Question,
    string Deck,
    string Status,
    string Due,
    string LastReviewed,
    string Recall,
    bool IsSelected)
{
    public long Id { get; init; }

    public string Answer { get; init; } = string.Empty;

    public string FrontContent { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool CanEdit => Id > 0 && !string.IsNullOrWhiteSpace(FrontContent);

    internal static CardLibraryRow Learning(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Learning", due, lastReviewed, recall);
    }

    internal static CardLibraryRow Relearning(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Relearning", due, lastReviewed, recall);
    }

    internal static CardLibraryRow Review(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Review", due, lastReviewed, recall);
    }

    private static CardLibraryRow Create(
        bool isSelected,
        string question,
        string deck,
        string status,
        string due,
        string lastReviewed,
        string recall)
    {
        return new(
            question,
            deck,
            status,
            due,
            lastReviewed,
            recall,
            isSelected);
    }
}

public sealed record CardFilterDeckRow(string Name, string Count, long? Id = null);

public sealed record CardFilterOptionRow(string Name, string Count, string? Value = null);

public sealed record CardEmptyStateRow(
    string Title,
    string Message);

public sealed record DeckPerformanceRow(
    string Deck,
    int CardsReviewed,
    int Retention,
    int Due,
    int Progress)
{
    public string RetentionLabel => string.Concat(Retention.ToString(CultureInfo.InvariantCulture), "%");

    public string ProgressLabel => string.Concat(Progress.ToString(CultureInfo.InvariantCulture), "%");
}
