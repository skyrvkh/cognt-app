using System.Globalization;
using System.Collections.ObjectModel;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private void RebuildFilterOptions(IReadOnlyList<KnowledgeNoteListItemReadModel> notes)
    {
        string[] previousTags = GetSelectedFilterValues(TagOptions, AllTagsValue);
        string[] previousTypes = GetSelectedFilterValues(TypeOptions, AllTypesValue);

        foreach (AllNotesFilterOptionViewModel option in TagOptions.Concat(TypeOptions))
        {
            option.PropertyChanged -= FilterOption_PropertyChanged;
        }

        isUpdatingFilterOptions = true;
        try
        {
            TagOptions.Clear();
            AddFilterOption(
                TagOptions,
                new AllNotesFilterOptionViewModel("All tags", AllTagsValue, previousTags.Length == 0));
            foreach (string tag in notes
                         .SelectMany(note => note.Tags)
                         .Where(tag => !string.IsNullOrWhiteSpace(tag))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
            {
                AddFilterOption(
                    TagOptions,
                    new AllNotesFilterOptionViewModel(tag, tag, previousTags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            }

            TypeOptions.Clear();
            AddFilterOption(
                TypeOptions,
                new AllNotesFilterOptionViewModel("All types", AllTypesValue, previousTypes.Length == 0));
            foreach (KnowledgeNoteListItemType type in notes
                         .Select(note => note.Type)
                         .Distinct()
                         .OrderBy(type => GetTypeLabel(type), StringComparer.OrdinalIgnoreCase))
            {
                AddFilterOption(
                    TypeOptions,
                    new AllNotesFilterOptionViewModel(
                        GetTypeLabel(type),
                        type.ToString(),
                        previousTypes.Contains(type.ToString(), StringComparer.OrdinalIgnoreCase)));
            }
        }
        finally
        {
            isUpdatingFilterOptions = false;
        }

        SelectedTagOption = TagOptions.FirstOrDefault(option => option.IsSelected) ?? TagOptions.FirstOrDefault();
        SelectedTypeOption = TypeOptions.FirstOrDefault(option => option.IsSelected) ?? TypeOptions.FirstOrDefault();
        SelectedSortOption ??= SortOptions.FirstOrDefault();
        RefreshFilterPresentationState();
    }

    private void UpdateEmptyState(int resultCount)
    {
        if (resultCount > 0)
        {
            EmptyStateTitle = string.Empty;
            EmptyStateMessage = string.Empty;
            return;
        }

        if (allNotes.Count == 0)
        {
            EmptyStateTitle = "No notes yet";
            EmptyStateMessage = "Create a note or import documents to get started.";
            return;
        }

        EmptyStateTitle = "No notes match your filters";
        EmptyStateMessage = "Try clearing search, tag, type, or sort filters.";
    }

    private AllNotesListItemViewModel ToListItemViewModel(KnowledgeNoteListItemReadModel note)
    {
        int cardCount = GetCardCount(note.RelativePath);
        return new AllNotesListItemViewModel(
            note.ItemId,
            note.RelativePath,
            note.Title,
            GetTypeLabel(note.Type),
            note.Tags.Count == 0 ? string.Empty : string.Join(", ", note.Tags),
            cardCount,
            FormatCardCount(cardCount),
            FormatTimestamp(note.CreatedAt),
            FormatTimestamp(note.UpdatedAt));
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value
            .ToLocalTime()
            .ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
    }

    private static string GetTypeLabel(KnowledgeNoteListItemType type)
    {
        return type switch
        {
            KnowledgeNoteListItemType.Markdown => "Markdown",
            KnowledgeNoteListItemType.PdfLinkedNote => "PDF-linked note",
            KnowledgeNoteListItemType.AttachmentBackedNote => "Attachment-backed note",
            _ => "Note"
        };
    }

    private static string[] GetSelectedFilterValues(
        IEnumerable<AllNotesFilterOptionViewModel> options,
        string allValue)
    {
        return options
            .Where(option => option.IsSelected && !string.Equals(option.Value, allValue, StringComparison.Ordinal))
            .Select(option => option.Value)
            .ToArray();
    }

    private int GetCardCount(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? 0
            : cardCountsBySourceRelativePath.TryGetValue(NormalizeSourceRelativePath(relativePath), out int count)
                ? count
                : 0;
    }

    private static string FormatCardCount(int cardCount)
    {
        return cardCount == 0
            ? string.Empty
            : cardCount.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeSourceRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim('/');
    }

    private static IReadOnlyList<AllNotesSortOptionViewModel> CreateSortOptions()
    {
        return
        [
            new AllNotesSortOptionViewModel("Sort: Last modified", AllNotesSortKey.ModifiedNewest),
            new AllNotesSortOptionViewModel("Modified oldest", AllNotesSortKey.ModifiedOldest),
            new AllNotesSortOptionViewModel("Created newest", AllNotesSortKey.CreatedNewest),
            new AllNotesSortOptionViewModel("Created oldest", AllNotesSortKey.CreatedOldest)
        ];
    }
}

public sealed record AllNotesListItemViewModel(
    long ItemId,
    string RelativePath,
    string Title,
    string TypeLabel,
    string TagsLabel,
    int CardCount,
    string CardCountLabel,
    string CreatedAtLabel,
    string ModifiedAtLabel)
{
    public bool HasCards => CardCount > 0;
}

public sealed class AllNotesTreeItemViewModel : ObservableObject
{
    private bool isExpanded;

    public AllNotesTreeItemViewModel(
        long id,
        KnowledgeItemKind kind,
        string title,
        string? relativePath,
        string typeLabel,
        string tagsLabel,
        int cardCount,
        string cardCountLabel,
        string createdAtLabel,
        string modifiedAtLabel,
        IEnumerable<AllNotesTreeItemViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeLabel);
        ArgumentNullException.ThrowIfNull(children);

        Id = id;
        Kind = kind;
        Title = title;
        RelativePath = relativePath;
        TypeLabel = typeLabel;
        TagsLabel = tagsLabel;
        CardCount = cardCount;
        CardCountLabel = cardCountLabel;
        CreatedAtLabel = createdAtLabel;
        ModifiedAtLabel = modifiedAtLabel;

        foreach (AllNotesTreeItemViewModel child in children)
        {
            Children.Add(child);
        }
    }

    public long Id { get; }

    public KnowledgeItemKind Kind { get; }

    public string Title { get; }

    public string? RelativePath { get; }

    public string TypeLabel { get; }

    public string TagsLabel { get; }

    public int CardCount { get; }

    public string CardCountLabel { get; }

    public string CreatedAtLabel { get; }

    public string ModifiedAtLabel { get; }

    public ObservableCollection<AllNotesTreeItemViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool CanOpen => Kind == KnowledgeItemKind.Note
        && !string.IsNullOrWhiteSpace(RelativePath);

    public bool HasCards => CardCount > 0
        && !string.IsNullOrWhiteSpace(RelativePath);

    public string Prefix => Kind switch
    {
        KnowledgeItemKind.Note => "[N]",
        KnowledgeItemKind.PdfDocument => "[PDF]",
        _ => "[F]"
    };
}

public sealed class AllNotesFilterOptionViewModel : ObservableObject
{
    private bool isSelected;

    public AllNotesFilterOptionViewModel(string label, string value, bool isSelected = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Label = label;
        Value = value;
        this.isSelected = isSelected;
    }

    public string Label { get; }

    public string Value { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

public sealed record AllNotesSortOptionViewModel(string Label, AllNotesSortKey SortKey);

public enum AllNotesSortKey
{
    ModifiedNewest,
    ModifiedOldest,
    CreatedNewest,
    CreatedOldest
}
