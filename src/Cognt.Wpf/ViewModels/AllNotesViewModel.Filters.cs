using System.Globalization;
using System.Collections.ObjectModel;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private void RebuildFilterOptions(IReadOnlyList<KnowledgeNoteListItemReadModel> notes)
    {
        HashSet<string> previousTagSet = CreateSelectedTagFilterSnapshot();
        HashSet<KnowledgeNoteListItemType> previousTypeSet = CreateSelectedTypeFilterSnapshot();

        HashSet<string> availableTagSet = new(StringComparer.OrdinalIgnoreCase);
        List<string> availableTags = [];
        HashSet<KnowledgeNoteListItemType> availableTypeSet = [];
        List<KnowledgeNoteListItemType> availableTypes = [];
        foreach (KnowledgeNoteListItemReadModel note in notes)
        {
            if (availableTypeSet.Add(note.Type))
            {
                availableTypes.Add(note.Type);
            }

            foreach (string tag in note.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && availableTagSet.Add(tag))
                {
                    availableTags.Add(tag);
                }
            }
        }

        availableTags.Sort(StringComparer.OrdinalIgnoreCase);
        availableTypes.Sort(CompareNoteTypes);

        List<(string Label, string Value, bool IsSelected)> nextTagOptions =
        [
            ("All tags", AllTagsValue, previousTagSet.Count == 0)
        ];
        foreach (string tag in availableTags)
        {
            nextTagOptions.Add((tag, tag, previousTagSet.Contains(tag)));
        }

        List<(string Label, string Value, bool IsSelected)> nextTypeOptions =
        [
            ("All types", AllTypesValue, previousTypeSet.Count == 0)
        ];
        foreach (KnowledgeNoteListItemType type in availableTypes)
        {
            string typeLabel = GetTypeLabel(type);
            string typeValue = type.ToString();
            nextTypeOptions.Add((typeLabel, typeValue, previousTypeSet.Contains(type)));
        }

        isUpdatingFilterOptions = true;
        try
        {
            SynchronizeFilterOptions(TagOptions, AllTagsValue, nextTagOptions);
            SynchronizeFilterOptions(TypeOptions, AllTypesValue, nextTypeOptions);
            EnsureAllOptionSelectedWhenNoConcreteSelection(TagOptions, AllTagsValue);
            EnsureAllOptionSelectedWhenNoConcreteSelection(TypeOptions, AllTypesValue);
        }
        finally
        {
            isUpdatingFilterOptions = false;
        }

        SyncTagFilterStateFromOptions();
        SyncTypeFilterStateFromOptions();
        SelectedTagOption = FindFirstSelectedOption(TagOptions);
        SelectedTypeOption = FindFirstSelectedOption(TypeOptions);
        SelectedSortOption ??= SortOptions.Count > 0 ? SortOptions[0] : null;
        RefreshFilterPresentationState();
    }

    private void SynchronizeFilterOptions(
        ObservableCollection<AllNotesFilterOptionViewModel> collection,
        string allValue,
        IReadOnlyList<(string Label, string Value, bool IsSelected)> nextOptions)
    {
        Dictionary<string, AllNotesFilterOptionViewModel> existingByValue = new(StringComparer.OrdinalIgnoreCase);
        foreach (AllNotesFilterOptionViewModel option in collection)
        {
            existingByValue[option.Value] = option;
        }

        List<AllNotesFilterOptionViewModel> reordered = [];
        foreach ((string Label, string Value, bool IsSelected) next in nextOptions)
        {
            if (!existingByValue.TryGetValue(next.Value, out AllNotesFilterOptionViewModel? option))
            {
                option = new AllNotesFilterOptionViewModel(next.Label, next.Value, next.IsSelected);
                option.PropertyChanged += FilterOption_PropertyChanged;
                filterOptionMetadata[option] = (collection, allValue);
            }
            else
            {
                option.Update(next.Label, next.Value, next.IsSelected);
                existingByValue.Remove(next.Value);
            }

            reordered.Add(option);
        }

        foreach (AllNotesFilterOptionViewModel removed in existingByValue.Values)
        {
            removed.PropertyChanged -= FilterOption_PropertyChanged;
            filterOptionMetadata.Remove(removed);
        }

        SynchronizeObservableCollection(collection, reordered);
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

    private static int CompareNoteTypes(
        KnowledgeNoteListItemType left,
        KnowledgeNoteListItemType right)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(GetTypeLabel(left), GetTypeLabel(right));
    }

    private static AllNotesFilterOptionViewModel? FindFirstSelectedOption(
        ObservableCollection<AllNotesFilterOptionViewModel> options)
    {
        foreach (AllNotesFilterOptionViewModel option in options)
        {
            if (option.IsSelected)
            {
                return option;
            }
        }

        return options.Count > 0 ? options[0] : null;
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
    private string label;
    private string optionValue;
    private bool isSelected;

    public AllNotesFilterOptionViewModel(string label, string value, bool isSelected = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        this.label = label;
        optionValue = value;
        this.isSelected = isSelected;
    }

    public string Label
    {
        get => label;
        private set => SetProperty(ref label, value);
    }

    public string Value
    {
        get => optionValue;
        private set => SetProperty(ref optionValue, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public void Update(string label, string value, bool isSelected = false)
    {
        Label = label;
        Value = value;
        IsSelected = isSelected;
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
