using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Views;

public partial class TagEditDialog : Window
{
    private readonly ObservableCollection<string> selectedTags = [];
    private readonly ObservableCollection<TagSuggestionViewModel> suggestions = [];
    private readonly List<string> availableTags = [];
    private readonly string[] initialAssignedTags;
    private bool suppressSearchTextChanged;

    public TagEditDialog(
        string title,
        KnowledgeTagOperationKind operation,
        IReadOnlyList<string> availableTags,
        IReadOnlyList<string> assignedTags,
        bool showScope = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(availableTags);
        ArgumentNullException.ThrowIfNull(assignedTags);

        InitializeComponent();
        Operation = operation;
        Title = "Edit Tags";
        ScopePanel.Visibility = showScope ? Visibility.Visible : Visibility.Collapsed;
        initialAssignedTags = NormalizeTags(assignedTags);

        IReadOnlyList<string> candidateTags = operation == KnowledgeTagOperationKind.Remove
            ? assignedTags
            : availableTags.Concat(assignedTags).ToArray();
        foreach (string tag in NormalizeTags(candidateTags)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            this.availableTags.Add(tag);
        }

        if (operation is KnowledgeTagOperationKind.Add or KnowledgeTagOperationKind.Remove)
        {
            AddSelectedTags(assignedTags);
        }

        SelectedTagsItemsControl.ItemsSource = selectedTags;
        TagSuggestionsListBox.ItemsSource = suggestions;
        UpdateSuggestions();
    }

    public KnowledgeTagOperationKind Operation { get; }

    public IReadOnlyList<string> SelectedTags { get; private set; } = [];

    public IReadOnlyList<string> RemovedTags { get; private set; } = [];

    public bool IncludeDescendants { get; private set; }

    private void TagSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        UpdateSuggestions();
    }

    private void TagSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressSearchTextChanged)
        {
            return;
        }

        ConsumeCompletedCommaEntries();
        UpdateSuggestions();
    }

    private void TagSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (TagSuggestionsListBox.SelectedItem is TagSuggestionViewModel selectedSuggestion)
            {
                SelectSuggestion(selectedSuggestion);
            }
            else
            {
                AddSearchTextAsTags();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemComma || e.Key == Key.Decimal)
        {
            AddSearchTextAsTags();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back && string.IsNullOrWhiteSpace(TagSearchTextBox.Text) && selectedTags.Count > 0)
        {
            selectedTags.RemoveAt(selectedTags.Count - 1);
            UpdateSuggestions();
        }
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TagSuggestionViewModel suggestion)
        {
            SelectSuggestion(suggestion);
        }
    }

    private void RemoveSelectedTag_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tag)
        {
            return;
        }

        string normalizedTag = NormalizeTag(tag);
        string? existing = selectedTags.FirstOrDefault(candidate =>
            string.Equals(candidate, normalizedTag, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            selectedTags.Remove(existing);
            ValidationText.Text = string.Empty;
            UpdateSuggestions();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        AddSearchTextAsTags();
        string[] selected = NormalizeTags(selectedTags).ToArray();
        if (Operation == KnowledgeTagOperationKind.Add)
        {
            string[] addedTags = selected
                .Where(tag => !initialAssignedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            string[] removedTags = initialAssignedTags
                .Where(tag => !selected.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (addedTags.Length == 0 && removedTags.Length == 0)
            {
                ValidationText.Text = "Add or remove at least one tag.";
                return;
            }

            SelectedTags = addedTags;
            RemovedTags = removedTags;
            IncludeDescendants = DescendantsRadioButton.IsChecked == true;
            DialogResult = true;
            return;
        }

        if (selected.Length == 0)
        {
            ValidationText.Text = "Select at least one assigned tag to remove.";
            return;
        }

        SelectedTags = selected;
        RemovedTags = [];
        IncludeDescendants = DescendantsRadioButton.IsChecked == true;
        DialogResult = true;
    }

    private void SelectSuggestion(TagSuggestionViewModel suggestion)
    {
        AddSelectedTag(suggestion.TagName);
        SetSearchText(string.Empty);
        TagSearchTextBox.Focus();
        ValidationText.Text = string.Empty;
        UpdateSuggestions();
    }

    private void AddSearchTextAsTags()
    {
        string[] tags = NormalizeTags(TagSearchTextBox.Text).ToArray();
        if (tags.Length == 0)
        {
            UpdateSuggestions();
            return;
        }

        AddSelectedTags(tags);
        SetSearchText(string.Empty);
        ValidationText.Text = string.Empty;
        UpdateSuggestions();
    }

    private void ConsumeCompletedCommaEntries()
    {
        string text = TagSearchTextBox.Text;
        int commaIndex = text.LastIndexOf(',');
        if (commaIndex < 0)
        {
            return;
        }

        string completedText = text[..commaIndex];
        string remainingText = text[(commaIndex + 1)..];
        AddSelectedTags(NormalizeTags(completedText));
        SetSearchText(remainingText);
    }

    private void AddSelectedTags(IEnumerable<string> tags)
    {
        foreach (string tag in NormalizeTags(tags))
        {
            AddSelectedTag(tag);
        }
    }

    private void AddSelectedTag(string tag)
    {
        string normalizedTag = NormalizeTag(tag);
        if (normalizedTag.Length == 0)
        {
            return;
        }

        if (!selectedTags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
        {
            selectedTags.Add(normalizedTag);
        }

        if (Operation == KnowledgeTagOperationKind.Add
            && !availableTags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
        {
            availableTags.Add(normalizedTag);
            availableTags.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void UpdateSuggestions()
    {
        string query = NormalizeTag(GetCurrentSearchTerm());
        string[] matchingTags = availableTags
            .Where(tag => !selectedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Where(tag => query.Length == 0
                || NormalizeTag(tag).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToArray();

        suggestions.Clear();
        foreach (string tag in matchingTags)
        {
            suggestions.Add(new TagSuggestionViewModel(tag, "Existing tag"));
        }

        if (Operation == KnowledgeTagOperationKind.Add
            && query.Length > 0
            && !availableTags.Contains(query, StringComparer.OrdinalIgnoreCase)
            && !selectedTags.Contains(query, StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new TagSuggestionViewModel(query, "Create new tag"));
        }

        TagSuggestionsPopup.IsOpen = TagSearchTextBox.IsKeyboardFocusWithin && suggestions.Count > 0;
    }

    private string GetCurrentSearchTerm()
    {
        string text = TagSearchTextBox.Text;
        int commaIndex = text.LastIndexOf(',');
        return commaIndex < 0 ? text : text[(commaIndex + 1)..];
    }

    private void SetSearchText(string value)
    {
        suppressSearchTextChanged = true;
        try
        {
            TagSearchTextBox.Text = value;
            TagSearchTextBox.CaretIndex = TagSearchTextBox.Text.Length;
        }
        finally
        {
            suppressSearchTextChanged = false;
        }
    }

    private static string NormalizeTag(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character)));
    }

    private static IEnumerable<string> NormalizeTags(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : NormalizeTags(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string[] NormalizeTags(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record TagSuggestionViewModel(string TagName, string Description)
{
    public string DisplayName { get; } = TagName;
}
