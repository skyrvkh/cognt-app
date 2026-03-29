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

public sealed record CardLibraryRow(
    bool IsSelected,
    string Question,
    string Deck,
    string Status,
    string Due,
    string LastReviewed,
    string Recall,
    string StatusBackgroundColor,
    string StatusBorderColor,
    string StatusForegroundColor,
    string RecallBackgroundColor,
    string RecallBorderColor,
    string RecallForegroundColor,
    string RowBackgroundColor)
{
    public Brush StatusBackground => BrushFrom(StatusBackgroundColor);

    public Brush StatusBorder => BrushFrom(StatusBorderColor);

    public Brush StatusForeground => BrushFrom(StatusForegroundColor);

    public Brush RecallBackground => BrushFrom(RecallBackgroundColor);

    public Brush RecallBorder => BrushFrom(RecallBorderColor);

    public Brush RecallForeground => BrushFrom(RecallForegroundColor);

    public Brush RowBackground => BrushFrom(RowBackgroundColor);

    public static CardLibraryRow Mastered(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Mastered", due, lastReviewed, recall, "#ECFDF3", "#A7F3D0", "#16A34A");
    }

    public static CardLibraryRow Learning(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Learning", due, lastReviewed, recall, "#FFF7ED", "#FED7AA", "#D97706");
    }

    public static CardLibraryRow DueCard(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "Due", due, lastReviewed, recall, "#FEF2F2", "#FECACA", "#DC2626");
    }

    public static CardLibraryRow New(bool isSelected, string question, string deck, string due, string lastReviewed, string recall)
    {
        return Create(isSelected, question, deck, "New", due, lastReviewed, recall, "#EFF6FF", "#BFDBFE", "#2563EB");
    }

    private static CardLibraryRow Create(
        bool isSelected,
        string question,
        string deck,
        string status,
        string due,
        string lastReviewed,
        string recall,
        string statusBackground,
        string statusBorder,
        string statusForeground)
    {
        var (recallBackground, recallBorder, recallForeground) = recall switch
        {
            "Easy" => ("#ECFDF3", "#A7F3D0", "#16A34A"),
            "Good" => ("#EFF6FF", "#BFDBFE", "#2563EB"),
            "Hard" => ("#FFF7ED", "#FED7AA", "#D97706"),
            _ => ("#FFFFFF", "#D9E1EE", "#667085")
        };

        return new(
            isSelected,
            question,
            deck,
            status,
            due,
            lastReviewed,
            recall,
            statusBackground,
            statusBorder,
            statusForeground,
            recallBackground,
            recallBorder,
            recallForeground,
            isSelected ? "#EEF2FF" : "#FFFFFF");
    }

    private static Brush BrushFrom(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}

public sealed record CardFilterDeckRow(string Name, string Count);

public sealed record DeckPerformanceRow(
    string Deck,
    int CardsReviewed,
    int Retention,
    int Due,
    int Progress)
{
    public string RetentionLabel => $"{Retention}%";

    public string ProgressLabel => $"{Progress}%";
}

public sealed record DailyStatisticsRow(
    string Date,
    int CardsReviewed,
    int MinutesStudied,
    int Recall)
{
    public string RecallLabel => $"{Recall}%";
}

public sealed record RecallDistributionRow(
    string Label,
    int Count,
    int Percent,
    string BarColor)
{
    public string PercentLabel => $"{Percent}%";

    public Brush BarBrush => (Brush)new BrushConverter().ConvertFromString(BarColor)!;
}

public sealed record WeeklyOutcomeRow(
    string Week,
    int Again,
    int Hard,
    int Good,
    int Easy)
{
    public string AgainLabel => $"{Again}%";

    public string HardLabel => $"{Hard}%";

    public string GoodLabel => $"{Good}%";

    public string EasyLabel => $"{Easy}%";
}

public sealed record StudyStreakRow(string Label, IReadOnlyList<StudyStreakCell> Cells);

public sealed record StudyStreakCell(int Cards);
