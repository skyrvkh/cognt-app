using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CogntApp.Views.Decks;

public partial class PostponeCardDialog : Window
{
    private const int MinimumDays = 1;
    private const int MaximumDays = 3650;
    private readonly DateTimeOffset[] dueDates;

    public PostponeCardDialog(IReadOnlyList<DateTimeOffset> dueDates)
    {
        ArgumentNullException.ThrowIfNull(dueDates);

        this.dueDates = dueDates.Count == 0
            ? [DateTimeOffset.UtcNow]
            : dueDates.ToArray();

        InitializeComponent();
        DaysTextBox.Text = "1";
        SummaryText.Text = this.dueDates.Length == 1
            ? "1 selected card"
            : $"{this.dueDates.Length} selected cards";
        CurrentDueText.Text = FormatDueRange(this.dueDates);
        UpdateValidation();
    }

    public int Days { get; private set; } = MinimumDays;

    private void DaysTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateValidation();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadDays(out int days))
        {
            UpdateValidation();
            return;
        }

        Days = days;
        DialogResult = true;
    }

    private void UpdateValidation()
    {
        bool isValid = TryReadDays(out int days);
        ConfirmButton.IsEnabled = isValid;
        ValidationText.Text = isValid
            ? string.Empty
            : $"Enter a whole number from {MinimumDays} to {MaximumDays}.";
        CalculatedDueText.Text = isValid
            ? FormatDueRange(dueDates.Select(due => due.AddDays(days)).ToArray())
            : "-";
    }

    private bool TryReadDays(out int days)
    {
        string value = DaysTextBox.Text.Trim();
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out days)
            && days is >= MinimumDays and <= MaximumDays;
    }

    private static string FormatDueRange(IReadOnlyList<DateTimeOffset> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        DateTimeOffset min = values.Min();
        DateTimeOffset max = values.Max();
        return min == max
            ? FormatDue(min)
            : $"{FormatDue(min)} to {FormatDue(max)}";
    }

    private static string FormatDue(DateTimeOffset value)
    {
        return value
            .ToLocalTime()
            .ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
    }
}
