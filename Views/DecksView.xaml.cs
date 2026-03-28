using System.Windows.Controls;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class DecksView : UserControl
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

    public DecksView()
    {
        InitializeComponent();
        DataContext = this;
    }
}

public sealed record DeckRow(
    bool IsSelected,
    string Name,
    string Category,
    int NewCards,
    int LearningCards,
    int DueCards,
    int TotalCards,
    int Progress,
    string BadgeBackgroundColor,
    string BadgeBorderColor,
    string BadgeForegroundColor,
    string RowBackgroundColor)
{
    public string ProgressLabel => $"{Progress}%";

    public Brush BadgeBackground => BrushFrom(BadgeBackgroundColor);

    public Brush BadgeBorder => BrushFrom(BadgeBorderColor);

    public Brush BadgeForeground => BrushFrom(BadgeForegroundColor);

    public Brush RowBackground => BrushFrom(RowBackgroundColor);

    private static Brush BrushFrom(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}

public sealed record CardRow(
    bool IsSelected,
    string Question,
    string Status,
    string LastReviewed,
    string StatusBackgroundColor,
    string StatusBorderColor,
    string StatusForegroundColor,
    string RowBackgroundColor)
{
    public Brush StatusBackground => BrushFrom(StatusBackgroundColor);

    public Brush StatusBorder => BrushFrom(StatusBorderColor);

    public Brush StatusForeground => BrushFrom(StatusForegroundColor);

    public Brush RowBackground => BrushFrom(RowBackgroundColor);

    public static CardRow Mastered(string question, string lastReviewed)
    {
        return new(false, question, "Mastered", lastReviewed, "#ECFDF3", "#A7F3D0", "#16A34A", "#FFFFFF");
    }

    public static CardRow Learning(string question, string lastReviewed)
    {
        return new(false, question, "Learning", lastReviewed, "#FFF7ED", "#FED7AA", "#D97706", "#FFFFFF");
    }

    public static CardRow Due(string question, string lastReviewed)
    {
        return new(false, question, "Due", lastReviewed, "#FEF2F2", "#FECACA", "#DC2626", "#FFFFFF");
    }

    public static CardRow New(string question, string lastReviewed)
    {
        return new(false, question, "New", lastReviewed, "#EFF6FF", "#BFDBFE", "#2563EB", "#FFFFFF");
    }

    private static Brush BrushFrom(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}
