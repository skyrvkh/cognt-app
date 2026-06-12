using System.Globalization;
using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.ViewModels;

namespace CogntApp.ViewModels.Decks.PresentationModels;

public sealed class DeckRow : ObservableObject
{
    private string name;
    private string displayName;
    private string parentPath;
    private string category;
    private int depth;
    private int learningQueueCards;
    private int dueCards;
    private int totalCards;
    private int progress;
    private bool isSelected;

    internal DeckRow(
        long id,
        string name,
        string displayName,
        string parentPath,
        string category,
        int depth,
        int learningQueueCards,
        int dueCards,
        int totalCards,
        int progress,
        bool isSelected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        Id = id;
        this.name = name;
        this.displayName = displayName;
        this.parentPath = parentPath;
        this.category = category;
        this.depth = depth;
        this.learningQueueCards = learningQueueCards;
        this.dueCards = dueCards;
        this.totalCards = totalCards;
        this.progress = progress;
        this.isSelected = isSelected;
    }

    public long Id { get; }

    public string Name
    {
        get => name;
        private set => SetProperty(ref name, value);
    }

    public string DisplayName
    {
        get => displayName;
        private set => SetProperty(ref displayName, value);
    }

    public string ParentPath
    {
        get => parentPath;
        private set
        {
            if (SetProperty(ref parentPath, value))
            {
                OnPropertyChanged(nameof(HasParent));
            }
        }
    }

    public string Category
    {
        get => category;
        private set => SetProperty(ref category, value);
    }

    public int Depth
    {
        get => depth;
        private set
        {
            if (SetProperty(ref depth, value))
            {
                OnPropertyChanged(nameof(TreeIndent));
            }
        }
    }

    public bool HasParent => !string.IsNullOrWhiteSpace(ParentPath);

    public Thickness TreeIndent => new(Math.Min(Depth, 8) * 18, 0, 0, 0);

    public int LearningQueueCards
    {
        get => learningQueueCards;
        private set => SetProperty(ref learningQueueCards, value);
    }

    public int DueCards
    {
        get => dueCards;
        private set => SetProperty(ref dueCards, value);
    }

    public int TotalCards
    {
        get => totalCards;
        private set => SetProperty(ref totalCards, value);
    }

    public int Progress
    {
        get => progress;
        private set
        {
            if (SetProperty(ref progress, value))
            {
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string ProgressLabel => string.Concat(Progress.ToString(CultureInfo.InvariantCulture), "%");

    internal void Update(
        string name,
        string displayName,
        string parentPath,
        string category,
        int depth,
        int learningQueueCards,
        int dueCards,
        int totalCards,
        int progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        Name = name;
        DisplayName = displayName;
        ParentPath = parentPath;
        Category = category;
        Depth = depth;
        LearningQueueCards = learningQueueCards;
        DueCards = dueCards;
        TotalCards = totalCards;
        Progress = progress;
    }
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
    string Recall)
{
    private const string BasicCardType = "basic";
    private const string ClozeCardType = "cloze";
    private const string MultimodalClozeCardType = "multimodal-cloze";

    public long Id { get; init; }

    public string Answer { get; init; } = string.Empty;

    public string FrontContent { get; init; } = string.Empty;

    public string BackContent { get; init; } = string.Empty;

    public string CardType { get; init; } = string.Empty;

    public FlashcardContentKind FrontContentKind { get; init; } = FlashcardContentKind.LegacyText;

    public FlashcardContentKind BackContentKind { get; init; } = FlashcardContentKind.LegacyText;

    public bool HasImageOcclusion { get; init; }

    public string? SourceRelPath { get; init; }

    public DateTimeOffset DueUtc { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string TagsLabel => Tags.Count == 0 ? string.Empty : string.Join(", ", Tags);

    public bool IsSuspended { get; init; }

    public bool IsBasicCard => string.Equals(CardType, BasicCardType, StringComparison.OrdinalIgnoreCase);

    public bool IsClozeCard => string.Equals(CardType, ClozeCardType, StringComparison.OrdinalIgnoreCase);

    public bool IsMultimodalCard => string.Equals(CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase);

    public bool IsEditableCardType => IsBasicCard || IsClozeCard || (IsMultimodalCard && !HasImageOcclusion);

    public bool CanEdit => Id > 0 && !string.IsNullOrWhiteSpace(FrontContent);

    public bool CanSchedule => Id > 0;

    public string SuspensionAction => IsSuspended ? "Unsuspend Card" : "Suspend Card";

    public string CardTypeLabel => IsBasicCard
        ? "Basic"
        : IsClozeCard
            ? "Cloze"
            : IsMultimodalCard
                ? HasImageOcclusion ? "Image/multimodal cloze" : "Text/code cloze"
                : string.IsNullOrWhiteSpace(CardType)
                    ? "Legacy"
                    : CardType;

    public string SourceLabel => string.IsNullOrWhiteSpace(SourceRelPath) ? "No source note" : SourceRelPath;

    public string SchedulingSummary => string.Concat(
        "Status: ",
        Status,
        " | Due: ",
        Due,
        " | Last reviewed: ",
        LastReviewed,
        " | Recall: ",
        Recall,
        IsSuspended ? " | Suspended" : string.Empty);

    internal static CardLibraryRow Learning(string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(question, deck, "Learning", due, lastReviewed, recall);
    }

    internal static CardLibraryRow Relearning(string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(question, deck, "Relearning", due, lastReviewed, recall);
    }

    internal static CardLibraryRow Review(string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(question, deck, "Review", due, lastReviewed, recall);
    }

    private static CardLibraryRow Create(
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
            recall);
    }
}

public sealed record CardEditResult(
    string FrontContent,
    string? BackContent,
    string DeckName,
    IReadOnlyList<string> Tags);

public sealed class CardFilterDeckRow : ObservableObject
{
    private string name;
    private string count;
    private bool isSelected;

    public CardFilterDeckRow(string name, string count, long? id = null, bool isSelected = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(count);

        this.name = name;
        this.count = count;
        Id = id;
        this.isSelected = isSelected;
    }

    public string Name
    {
        get => name;
        private set => SetProperty(ref name, value);
    }

    public string Count
    {
        get => count;
        private set => SetProperty(ref count, value);
    }

    public long? Id { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    internal void Update(string name, string count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(count);

        Name = name;
        Count = count;
    }
}

public sealed class CardFilterOptionRow : ObservableObject
{
    private string name;
    private string count;
    private bool isSelected;

    public CardFilterOptionRow(string name, string count, string? value = null, bool isSelected = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(count);

        this.name = name;
        this.count = count;
        Value = value;
        this.isSelected = isSelected;
    }

    public string Name
    {
        get => name;
        private set => SetProperty(ref name, value);
    }

    public string Count
    {
        get => count;
        private set => SetProperty(ref count, value);
    }

    public string? Value { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    internal void Update(string name, string count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(count);

        Name = name;
        Count = count;
    }
}

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
