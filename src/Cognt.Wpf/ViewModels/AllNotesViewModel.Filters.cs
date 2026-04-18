using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel
{
    private void RebuildFilterOptions(IReadOnlyList<KnowledgeNoteListItemReadModel> notes)
    {
        string? previousTag = SelectedTagOption?.Value;
        string? previousType = SelectedTypeOption?.Value;

        TagOptions.Clear();
        TagOptions.Add(new AllNotesFilterOptionViewModel("All tags", AllTagsValue));
        foreach (string tag in notes
                     .SelectMany(note => note.Tags)
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            TagOptions.Add(new AllNotesFilterOptionViewModel(tag, tag));
        }

        TypeOptions.Clear();
        TypeOptions.Add(new AllNotesFilterOptionViewModel("All types", AllTypesValue));
        foreach (KnowledgeNoteListItemType type in notes
                     .Select(note => note.Type)
                     .Distinct()
                     .OrderBy(type => GetTypeLabel(type), StringComparer.OrdinalIgnoreCase))
        {
            TypeOptions.Add(new AllNotesFilterOptionViewModel(GetTypeLabel(type), type.ToString()));
        }

        SelectedTagOption = FindFilterOption(TagOptions, previousTag) ?? TagOptions.FirstOrDefault();
        SelectedTypeOption = FindFilterOption(TypeOptions, previousType) ?? TypeOptions.FirstOrDefault();
        SelectedSortOption ??= SortOptions.FirstOrDefault();
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

    private static AllNotesListItemViewModel ToListItemViewModel(KnowledgeNoteListItemReadModel note)
    {
        return new AllNotesListItemViewModel(
            note.RelativePath,
            note.Title,
            GetTypeLabel(note.Type),
            GetLocationLabel(note.RelativePath),
            note.Tags.Count == 0 ? string.Empty : string.Join(", ", note.Tags),
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

    private static string GetLocationLabel(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "Workspace root";
        }

        string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string? directory = Path.GetDirectoryName(normalizedPath);
        return string.IsNullOrWhiteSpace(directory)
            ? "Workspace root"
            : directory.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string? NormalizeFilterValue(string? value, string allValue)
    {
        return string.Equals(value, allValue, StringComparison.Ordinal)
            ? null
            : value;
    }

    private static AllNotesFilterOptionViewModel? FindFilterOption(
        IEnumerable<AllNotesFilterOptionViewModel> options,
        string? value)
    {
        return options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
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
    string RelativePath,
    string Title,
    string TypeLabel,
    string LocationLabel,
    string TagsLabel,
    string CreatedAtLabel,
    string ModifiedAtLabel);

public sealed class AllNotesTreeItemViewModel : ObservableObject
{
    private bool isExpanded;

    public AllNotesTreeItemViewModel(
        long id,
        KnowledgeItemKind kind,
        string title,
        string? relativePath,
        string typeLabel,
        string locationLabel,
        string createdAtLabel,
        string modifiedAtLabel,
        IEnumerable<AllNotesTreeItemViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationLabel);
        ArgumentNullException.ThrowIfNull(children);

        Id = id;
        Kind = kind;
        Title = title;
        RelativePath = relativePath;
        TypeLabel = typeLabel;
        LocationLabel = locationLabel;
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

    public string LocationLabel { get; }

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

    public string Prefix => Kind switch
    {
        KnowledgeItemKind.Note => "[N]",
        KnowledgeItemKind.PdfDocument => "[PDF]",
        _ => "[F]"
    };
}

public sealed record AllNotesFilterOptionViewModel(string Label, string Value);

public sealed record AllNotesSortOptionViewModel(string Label, AllNotesSortKey SortKey);

public enum AllNotesSortKey
{
    ModifiedNewest,
    ModifiedOldest,
    CreatedNewest,
    CreatedOldest
}
