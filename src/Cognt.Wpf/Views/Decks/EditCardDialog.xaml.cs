using System.Windows;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels.Decks.PresentationModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Views.Decks;

public partial class EditCardDialog : Window
{
    private static ILoggerFactory? loggerFactory;
    private readonly Func<EditorView> createEditorView;
    private readonly CardLibraryRow card;
    private readonly ILogger<EditCardDialog>? logger;
    private EditorView? frontEditorView;
    private EditorView? backEditorView;
    private string? frontEditorContent;
    private string? backEditorContent;
    private bool isEditorLoaded;

    public EditCardDialog(
        Func<EditorView> createEditorView,
        CardLibraryRow card,
        IReadOnlyList<string> deckNames)
    {
        ArgumentNullException.ThrowIfNull(createEditorView);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(deckNames);

        logger = loggerFactory?.CreateLogger<EditCardDialog>();
        this.createEditorView = createEditorView;
        this.card = card;

        InitializeComponent();

        DeckComboBox.ItemsSource = deckNames;
        DeckComboBox.Text = card.Deck;
        TagsTextBox.Text = string.Join(", ", card.Tags);
        SetFooterStatus();
        BackTab.Visibility = card.IsBasicCard ? Visibility.Visible : Visibility.Collapsed;

        if (!card.IsEditableCardType)
        {
            EditorTabs.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Visible;
            UnsupportedText.Text = card.IsMultimodalCard
                ? "Image occlusion cards are not editable in this text editor yet. Use the original note/image mask workflow so masks, slots, and targets stay consistent."
                : "This stored card format is not a supported Basic or Cloze card document.";
            SaveButton.IsEnabled = false;
            SetFooterStatus("This card is read-only here.");
        }

        Loaded += EditCardDialog_Loaded;
        Closed += EditCardDialog_Closed;
    }

    public CardEditResult? EditResult { get; private set; }

    internal static void ConfigureLogging(ILoggerFactory factory)
    {
        loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

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
            WireCardEditor(frontEditorView);
            FrontEditorHost.Content = frontEditorView;
            await frontEditorView.LoadNoteAsync(
                card.IsClozeCard ? "Edit Cloze Source" : "Edit Front",
                card.FrontContent,
                $"card-edit-{card.Id}-front.json");

            if (card.IsBasicCard)
            {
                backEditorView = createEditorView();
                WireCardEditor(backEditorView);
                BackEditorHost.Content = backEditorView;
                await backEditorView.LoadNoteAsync(
                    "Edit Back",
                    string.IsNullOrWhiteSpace(card.BackContent) ? card.Answer : card.BackContent,
                    $"card-edit-{card.Id}-back.json");
            }

            SetBusy(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Edit card dialog failed to load editor views. CardId={CardId}", card.Id);
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
            SetFooterStatus("Deck is required.");
            return;
        }

        SetBusy(true, "Saving card...");
        try
        {
            string frontContent = await GetEditorContentAsync(frontEditorView, frontEditorContent);
            string? backContent = card.IsBasicCard && backEditorView is not null
                ? await GetEditorContentAsync(backEditorView, backEditorContent)
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
            logger?.LogError(ex, "Edit card save failed. CardId={CardId}", card.Id);
            SetBusy(false, $"Card could not be exported: {ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void EditCardDialog_Closed(object? sender, EventArgs e)
    {
        UnwireCardEditor(frontEditorView);
        UnwireCardEditor(backEditorView);
        frontEditorView?.DisposeEditorHost();
        backEditorView?.DisposeEditorHost();
        FrontEditorHost.Content = null;
        BackEditorHost.Content = null;
    }

    private void SetBusy(bool isBusy, string message)
    {
        SaveButton.IsEnabled = !isBusy && card.IsEditableCardType;
        SetFooterStatus(message);
    }

    private void SetBusy(bool isBusy)
    {
        SaveButton.IsEnabled = !isBusy && card.IsEditableCardType;
        SetFooterStatus();
    }

    private void SetFooterStatus(string? message = null)
    {
        StatusText.Text = string.IsNullOrWhiteSpace(message)
            ? card.SchedulingSummary
            : message;
    }

    private void WireCardEditor(EditorView editorView)
    {
        editorView.ContentChanged += EditorView_ContentChanged;
        editorView.SaveClozeRequested += EditorView_SaveClozeRequested;
    }

    private void UnwireCardEditor(EditorView? editorView)
    {
        if (editorView is null)
        {
            return;
        }

        editorView.ContentChanged -= EditorView_ContentChanged;
        editorView.SaveClozeRequested -= EditorView_SaveClozeRequested;
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        if (ReferenceEquals(sender, frontEditorView))
        {
            frontEditorContent = content;
            return;
        }

        if (ReferenceEquals(sender, backEditorView))
        {
            backEditorContent = content;
            return;
        }
    }

    private void EditorView_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        EditorView_ContentChanged(sender, e.Content);
    }

    private static async Task<string> GetEditorContentAsync(EditorView editorView, string? fallbackContent)
    {
        string content = await editorView.GetContentAsync();
        return string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(fallbackContent)
            ? fallbackContent
            : content;
    }

    private static string[] ParseTags(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> tags = [];
        char[] tagBuffer = new char[value.Length];
        int tagLength = 0;

        foreach (char character in value)
        {
            if (IsTagDelimiter(character))
            {
                AddBufferedTag(tagBuffer, tagLength, seenTags, tags);
                tagLength = 0;
                continue;
            }

            if (!char.IsWhiteSpace(character))
            {
                tagBuffer[tagLength++] = character;
            }
        }

        AddBufferedTag(tagBuffer, tagLength, seenTags, tags);
        tags.Sort(StringComparer.OrdinalIgnoreCase);
        return tags.ToArray();
    }

    private static bool IsTagDelimiter(char character)
    {
        return character is ',' or ';' or '\r' or '\n';
    }

    private static void AddBufferedTag(
        char[] tagBuffer,
        int tagLength,
        HashSet<string> seenTags,
        List<string> tags)
    {
        if (tagLength == 0)
        {
            return;
        }

        string tag = new(tagBuffer, 0, tagLength);
        if (seenTags.Add(tag))
        {
            tags.Add(tag);
        }
    }
}
