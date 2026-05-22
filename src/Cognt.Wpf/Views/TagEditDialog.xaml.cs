using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Views;

public partial class TagEditDialog : Window
{
    private readonly ObservableCollection<TagChoiceViewModel> tags = [];

    public TagEditDialog(
        string title,
        KnowledgeTagOperationKind operation,
        IReadOnlyList<string> availableTags,
        IReadOnlyList<string> assignedTags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(availableTags);
        ArgumentNullException.ThrowIfNull(assignedTags);

        InitializeComponent();
        Operation = operation;
        TitleText.Text = operation == KnowledgeTagOperationKind.Add ? "Add Tags" : "Remove Tags";
        SubtitleText.Text = $"{title} - {(operation == KnowledgeTagOperationKind.Add ? "select or create tags to assign." : "select assigned tags to remove.")}";

        string[] initialTags = operation == KnowledgeTagOperationKind.Remove
            ? assignedTags.ToArray()
            : availableTags.Concat(assignedTags).ToArray();
        foreach (string tag in initialTags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(new TagChoiceViewModel(tag, operation == KnowledgeTagOperationKind.Remove));
        }

        TagItemsControl.ItemsSource = tags;
    }

    public KnowledgeTagOperationKind Operation { get; }

    public IReadOnlyList<string> SelectedTags { get; private set; } = [];

    public bool IncludeDescendants { get; private set; }

    private void PendingTagTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddPendingTag();
            e.Handled = true;
        }
    }

    private void AddPendingTag_Click(object sender, RoutedEventArgs e)
    {
        AddPendingTag();
    }

    private void AddPendingTag()
    {
        string[] pendingTags = NormalizeTags(PendingTagTextBox.Text);
        if (pendingTags.Length == 0)
        {
            return;
        }

        foreach (string tag in pendingTags)
        {
            TagChoiceViewModel? existing = tags.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayName, tag, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                tags.Add(new TagChoiceViewModel(tag, isSelected: true));
            }
            else
            {
                existing.IsSelected = true;
            }
        }

        PendingTagTextBox.Text = string.Empty;
        ValidationText.Text = string.Empty;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        AddPendingTag();
        string[] selected = tags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.DisplayName)
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selected.Length == 0)
        {
            ValidationText.Text = "Select or create at least one tag.";
            return;
        }

        SelectedTags = selected;
        IncludeDescendants = DescendantsRadioButton.IsChecked == true;
        DialogResult = true;
    }

    private static string NormalizeTag(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character)));
    }

    private static string[] NormalizeTags(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeTag)
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}

public sealed class TagChoiceViewModel : CogntApp.ViewModels.ObservableObject
{
    private bool isSelected;

    public TagChoiceViewModel(string displayName, bool isSelected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName;
        this.isSelected = isSelected;
    }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
