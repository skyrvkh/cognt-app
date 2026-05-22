using System.Collections.ObjectModel;
using System.Windows.Input;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed class CreateCardReviewSubviewViewModel : ObservableObject
{
    private const string DefaultDeckName = "Default";
    private const string DefaultSourceLabel = "No source";

    private string selectedDeckName = DefaultDeckName;
    private string pendingTagText = string.Empty;
    private string sourceLabel = DefaultSourceLabel;

    public CreateCardReviewSubviewViewModel(Action backToCards, string initialCardContent)
    {
        ArgumentNullException.ThrowIfNull(backToCards);
        ArgumentNullException.ThrowIfNull(initialCardContent);

        BackToCardsCommand = new RelayCommand(backToCards);
        InitialCardContent = initialCardContent;
        DeckNames.Add(selectedDeckName);
    }

    public ICommand BackToCardsCommand { get; }

    public ObservableCollection<string> DeckNames { get; } = [];

    public ObservableCollection<string> Tags { get; } = [];

    public string SelectedDeckName
    {
        get => selectedDeckName;
        set => SetProperty(ref selectedDeckName, string.IsNullOrWhiteSpace(value) ? DefaultDeckName : value);
    }

    public string PendingTagText
    {
        get => pendingTagText;
        set => SetProperty(ref pendingTagText, value);
    }

    public string SourceLabel
    {
        get => sourceLabel;
        private set => SetProperty(ref sourceLabel, string.IsNullOrWhiteSpace(value) ? DefaultSourceLabel : value);
    }

    public string InitialCardContent { get; }

    internal CreateCardsRequestedEventArgs BuildCreateRequest(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        CommitPendingTags();
        return new CreateCardsRequestedEventArgs(
            SelectedDeckName,
            content,
            Tags.ToArray());
    }

    public void LoadDecks(IEnumerable<string> deckNames)
    {
        string previousSelection = selectedDeckName;
        DeckNames.Clear();

        foreach (string deckName in deckNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DeckNames.Add(deckName);
        }

        if (DeckNames.Count == 0)
        {
            DeckNames.Add(DefaultDeckName);
        }

        SelectedDeckName = ContainsDeckName(previousSelection) ? previousSelection : DeckNames[0];
    }

    public void SetSource(string? sourceLabel)
    {
        SourceLabel = sourceLabel ?? string.Empty;
    }

    public void CommitPendingTags()
    {
        AddTags(PendingTagText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        PendingTagText = string.Empty;
    }

    public void CommitDelimitedTags()
    {
        if (!PendingTagText.Contains(','))
        {
            return;
        }

        string[] parts = PendingTagText.Split(',');
        AddTags(parts.Take(parts.Length - 1));
        PendingTagText = parts[^1].Trim();
    }

    internal bool TryHandlePendingTagShortcut(Key key)
    {
        if (key is not (Key.Enter or Key.OemComma))
        {
            return false;
        }

        CommitPendingTags();
        return true;
    }

    private void AddTags(IEnumerable<string> tags)
    {
        foreach (string tag in tags
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeTagDisplayName)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                Tags.Add(tag);
            }
        }
    }

    private bool ContainsDeckName(string deckName)
    {
        return DeckNames.Contains(deckName, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }
}
