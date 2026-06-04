using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using static CogntApp.Views.TagEditDialogTagNormalizer;

namespace CogntApp.Views;

public partial class TagEditDialog : Window
{
    private readonly ObservableCollection<string> selectedTags = [];
    private readonly ObservableCollection<TagSuggestionViewModel> suggestions = [];
    private readonly List<string> availableTags = [];
    private readonly HashSet<string> selectedTagSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> availableTagSet = new(StringComparer.OrdinalIgnoreCase);
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
        TargetTitleText.Text = title;
        ScopePanel.Visibility = showScope ? Visibility.Visible : Visibility.Collapsed;
        initialAssignedTags = NormalizeTags(assignedTags);

        if (operation == KnowledgeTagOperationKind.Remove)
        {
            AddAvailableTags(assignedTags);
        }
        else
        {
            AddAvailableTags(availableTags);
            AddAvailableTags(assignedTags);
        }

        this.availableTags.Sort(StringComparer.OrdinalIgnoreCase);

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
            RemoveLastSelectedTag();
            UpdateSuggestions();
        }
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TagSuggestionViewModel suggestion)
        {
            SelectSuggestion(suggestion);
            return;
        }
    }

    private void RemoveSelectedTag_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tag)
        {
            return;
        }

        string normalizedTag = NormalizeTag(tag);
        if (TryRemoveSelectedTag(normalizedTag))
        {
            ValidationText.Text = string.Empty;
            UpdateSuggestions();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        AddSearchTextAsTags();
        string[] selected = NormalizeTags(selectedTags);
        if (Operation == KnowledgeTagOperationKind.Add)
        {
            string[] addedTags = ExceptTags(selected, initialAssignedTags);
            string[] removedTags = ExceptTags(initialAssignedTags, selected);

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
        string[] tags = NormalizeTags(TagSearchTextBox.Text);
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
        string[] completedTags = NormalizeTags(completedText);
        AddSelectedTags(completedTags);
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

        if (selectedTagSet.Add(normalizedTag))
        {
            selectedTags.Add(normalizedTag);
        }

        if (Operation == KnowledgeTagOperationKind.Add
            && availableTagSet.Add(normalizedTag))
        {
            availableTags.Add(normalizedTag);
            availableTags.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void UpdateSuggestions()
    {
        string query = NormalizeTag(GetCurrentSearchTerm());
        suggestions.Clear();
        foreach (string tag in availableTags)
        {
            if (selectedTagSet.Contains(tag)
                || (query.Length > 0 && !tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            suggestions.Add(new TagSuggestionViewModel(tag, "Existing tag"));
            if (suggestions.Count >= 10)
            {
                break;
            }
        }

        if (Operation == KnowledgeTagOperationKind.Add
            && query.Length > 0
            && !availableTagSet.Contains(query)
            && !selectedTagSet.Contains(query))
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

    private void AddAvailableTags(IEnumerable<string> tags)
    {
        foreach (string tag in NormalizeTags(tags))
        {
            if (availableTagSet.Add(tag))
            {
                availableTags.Add(tag);
            }
        }
    }

    private void RemoveLastSelectedTag()
    {
        if (selectedTags.Count == 0)
        {
            return;
        }

        string tag = selectedTags[selectedTags.Count - 1];
        selectedTags.RemoveAt(selectedTags.Count - 1);
        selectedTagSet.Remove(tag);
    }

    private bool TryRemoveSelectedTag(string tag)
    {
        if (!selectedTagSet.Contains(tag))
        {
            return false;
        }

        for (int index = 0; index < selectedTags.Count; index++)
        {
            if (string.Equals(selectedTags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                selectedTags.RemoveAt(index);
                selectedTagSet.Remove(tag);
                return true;
            }
        }

        selectedTagSet.Remove(tag);
        return false;
    }

}

public sealed record TagSuggestionViewModel(string TagName, string Description)
{
    public string DisplayName { get; } = TagName;
}

