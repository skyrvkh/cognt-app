using System.Windows.Media;

namespace CogntApp.Models.Decks;

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

public sealed record RecallDistributionRow(
    string Label,
    int Count,
    int Percent,
    string BarColor)
{
    public string PercentLabel => $"{Percent}%";
}