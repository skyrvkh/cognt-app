using System.Windows;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Views.Decks;

public partial class EditCardDialog : Window
{
    private readonly Func<EditorView> createEditorView;
    private readonly CardLibraryRow card;
    private EditorView? frontEditorView;
    private EditorView? backEditorView;
    private bool isEditorLoaded;

    public EditCardDialog(
        Func<EditorView> createEditorView,
        CardLibraryRow card,
        IReadOnlyList<string> deckNames)
    {
        ArgumentNullException.ThrowIfNull(createEditorView);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(deckNames);

        this.createEditorView = createEditorView;
        this.card = card;

        InitializeComponent();

        DeckComboBox.ItemsSource = deckNames;
        DeckComboBox.Text = card.Deck;
        TagsTextBox.Text = string.Join(", ", card.Tags);
        CardTypeText.Text = card.CardTypeLabel;
        CardSummaryText.Text = string.IsNullOrWhiteSpace(card.Question)
            ? $"Card #{card.Id}"
            : card.Question;
        SourceText.Text = card.SourceLabel;
        SchedulingText.Text = card.SchedulingSummary;
        BackTab.Visibility = card.IsBasicCard ? Visibility.Visible : Visibility.Collapsed;

        if (!card.IsEditableCardType)
        {
            EditorTabs.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Visible;
            UnsupportedText.Text = card.IsMultimodalCard
                ? "Image occlusion and multimodal cloze cards should be edited from the note-to-card queue so masks, slots, and targets stay consistent."
                : "This stored card format is not a supported Basic or Cloze card document.";
            SaveButton.IsEnabled = false;
        }

        Loaded += EditCardDialog_Loaded;
        Closed += EditCardDialog_Closed;
    }

    public CardEditResult? EditResult { get; private set; }

    private async void EditCardDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (isEditorLoaded || !card.IsEditableCardType)
        {
            return;
        }

        isEditorLoaded = true;
        SetBusy(true, "Loading card editor...");
        try
        {
            frontEditorView = createEditorView();
            FrontEditorHost.Content = frontEditorView;
            await frontEditorView.LoadNoteAsync(
                card.IsClozeCard ? "Edit Cloze Source" : "Edit Front",
                card.FrontContent,
                $"card-edit-{card.Id}-front.json");

            if (card.IsBasicCard)
            {
                backEditorView = createEditorView();
                BackEditorHost.Content = backEditorView;
                await backEditorView.LoadNoteAsync(
                    "Edit Back",
                    string.IsNullOrWhiteSpace(card.BackContent) ? card.Answer : card.BackContent,
                    $"card-edit-{card.Id}-back.json");
            }

            SetBusy(false, string.Empty);
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Card editor could not load: {ex.Message}");
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!card.IsEditableCardType || frontEditorView is null)
        {
            return;
        }

        string deckName = DeckComboBox.Text?.Trim() ?? string.Empty;
        if (deckName.Length == 0)
        {
            StatusText.Text = "Deck is required.";
            return;
        }

        SetBusy(true, "Saving card...");
        try
        {
            string frontContent = await frontEditorView.GetContentAsync();
            string? backContent = card.IsBasicCard && backEditorView is not null
                ? await backEditorView.GetContentAsync()
                : null;
            EditResult = new CardEditResult(
                frontContent,
                backContent,
                deckName,
                ParseTags(TagsTextBox.Text));
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Card could not be exported: {ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void EditCardDialog_Closed(object? sender, EventArgs e)
    {
        FrontEditorHost.Content = null;
        BackEditorHost.Content = null;
    }

    private void SetBusy(bool isBusy, string message)
    {
        SaveButton.IsEnabled = !isBusy && card.IsEditableCardType;
        StatusText.Text = message;
    }

    private static string[] ParseTags(string? value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => string.Concat(tag.Where(character => !char.IsWhiteSpace(character))))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
