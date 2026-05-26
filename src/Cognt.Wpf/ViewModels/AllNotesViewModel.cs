using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class AllNotesViewModel : ObservableObject, IDisposable
{
    private const string AllTagsValue = "__all_tags";
    private const string AllTypesValue = "__all_types";
    private readonly INoteService noteService;
    private readonly IFlashcardCardsQuery flashcardCardsQuery;
    private readonly RelayCommand openNoteCommand;
    private readonly RelayCommand showCardsCommand;
    private readonly RelayCommand addTagsCommand;
    private readonly RelayCommand removeTagsCommand;
    private string currentCollectionId = string.Empty;
    private string searchQuery = string.Empty;
    private bool isLoading;
    private bool hasError;
    private string errorMessage = string.Empty;
    private string emptyStateTitle = "No notes yet.";
    private string emptyStateMessage = "Create or import a note to get started.";
    private string typeFilterLabel = "Any type";
    private string tagFilterLabel = "Any tag";
    private bool hasSearchQuery;
    private bool hasSelectedTypeFilters;
    private bool hasSelectedTagFilters;
    private bool hasActiveFilters;
    private IReadOnlyList<AllNotesFilterOptionViewModel> activeTypeFilters = [];
    private IReadOnlyList<AllNotesFilterOptionViewModel> activeTagFilters = [];
    private bool hasLoadedCatalog;
    private AllNotesFilterOptionViewModel? selectedTagOption;
    private AllNotesFilterOptionViewModel? selectedTypeOption;
    private AllNotesSortOptionViewModel? selectedSortOption;
    private CancellationTokenSource? applyFiltersCancellation;
    private IReadOnlyList<KnowledgeNoteListItemReadModel> allNotes = [];
    private IReadOnlyList<AllNotesListItemViewModel> allNoteListItems = [];
    private IReadOnlyList<AllNotesTreeItemViewModel> allTreeRoots = [];
    private Dictionary<string, int> cardCountsBySourceRelativePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AllNotesFilterOptionViewModel, (ObservableCollection<AllNotesFilterOptionViewModel> Owner, string AllValue)> filterOptionMetadata = [];
    private readonly HashSet<KnowledgeNoteListItemType> selectedTypeFilterSet = [];
    private readonly HashSet<string> selectedTagFilterSet = new(StringComparer.OrdinalIgnoreCase);
    private bool isTreeViewMode;
    private bool isUpdatingFilterOptions;

    public AllNotesViewModel(INoteService noteService, IFlashcardCardsQuery flashcardCardsQuery)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(flashcardCardsQuery);

        this.noteService = noteService;
        this.flashcardCardsQuery = flashcardCardsQuery;
        openNoteCommand = new RelayCommand(OpenNoteFromCommand, CanOpenNoteFromCommand);
        showCardsCommand = new RelayCommand(ShowCardsFromCommand, CanShowCardsFromCommand);
        addTagsCommand = new RelayCommand(parameter => RequestTagEdit(parameter, KnowledgeTagOperationKind.Add), CanRequestTagEdit);
        removeTagsCommand = new RelayCommand(parameter => RequestTagEdit(parameter, KnowledgeTagOperationKind.Remove), CanRequestTagEdit);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        ClearTagFiltersCommand = new RelayCommand(() => SelectOnlyAllFilterOption(TagOptions, AllTagsValue));
        ClearTypeFiltersCommand = new RelayCommand(() => SelectOnlyAllFilterOption(TypeOptions, AllTypesValue));
        CreateNoteCommand = new RelayCommand(() => CreateNoteRequested?.Invoke(this, EventArgs.Empty));
        ImportDocumentsCommand = new RelayCommand(() => ImportDocumentsRequested?.Invoke(this, EventArgs.Empty));
        ShowDataGridViewCommand = new RelayCommand(() => IsTreeViewMode = false);
        ShowTreeViewCommand = new RelayCommand(() => IsTreeViewMode = true);

        AddFilterOption(TagOptions, AllTagsValue, new AllNotesFilterOptionViewModel("All tags", AllTagsValue, isSelected: true));
        AddFilterOption(TypeOptions, AllTypesValue, new AllNotesFilterOptionViewModel("All types", AllTypesValue, isSelected: true));

        foreach (AllNotesSortOptionViewModel option in CreateSortOptions())
        {
            SortOptions.Add(option);
        }

        selectedTagOption = TagOptions[0];
        selectedTypeOption = TypeOptions[0];
        selectedSortOption = SortOptions[0];
        RefreshFilterPresentationState();
    }

    internal event EventHandler<OpenAllNotesRequestedEventArgs>? OpenNoteRequested;

    internal event EventHandler<OpenNoteCardsRequestedEventArgs>? ShowCardsRequested;

    internal event EventHandler<TagEditRequestedEventArgs>? TagEditRequested;

    internal event EventHandler? CreateNoteRequested;

    internal event EventHandler? ImportDocumentsRequested;

    public void Dispose()
    {
        applyFiltersCancellation?.Cancel();
        applyFiltersCancellation?.Dispose();
        applyFiltersCancellation = null;
    }

    public ObservableCollection<AllNotesListItemViewModel> VisibleNotes { get; } = [];

    public ObservableCollection<AllNotesTreeItemViewModel> VisibleTreeNodes { get; } = [];

    public ObservableCollection<AllNotesFilterOptionViewModel> TagOptions { get; } = [];

    public ObservableCollection<AllNotesFilterOptionViewModel> TypeOptions { get; } = [];

    public ObservableCollection<AllNotesSortOptionViewModel> SortOptions { get; } = [];

    public ICommand OpenNoteCommand => openNoteCommand;

    public ICommand ShowCardsCommand => showCardsCommand;

    public ICommand AddTagsCommand => addTagsCommand;

    public ICommand RemoveTagsCommand => removeTagsCommand;

    public ICommand ClearFiltersCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public ICommand ClearTagFiltersCommand { get; }

    public ICommand ClearTypeFiltersCommand { get; }

    public ICommand CreateNoteCommand { get; }

    public ICommand ImportDocumentsCommand { get; }

    public ICommand ShowDataGridViewCommand { get; }

    public ICommand ShowTreeViewCommand { get; }

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (SetProperty(ref searchQuery, value))
            {
                UpdateSearchFilterState();
                QueueApplyFilters();
            }
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
            {
                OnPropertyChanged(nameof(ShouldShowEmptyState));
            }
        }
    }

    public bool HasError
    {
        get => hasError;
        private set
        {
            if (SetProperty(ref hasError, value))
            {
                OnPropertyChanged(nameof(ShouldShowEmptyState));
            }
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public string EmptyStateTitle
    {
        get => emptyStateTitle;
        private set => SetProperty(ref emptyStateTitle, value);
    }

    public string EmptyStateMessage
    {
        get => emptyStateMessage;
        private set => SetProperty(ref emptyStateMessage, value);
    }

    public bool HasVisibleNotes => VisibleNotes.Count > 0;

    public bool ShouldShowEmptyState => !IsLoading && !HasError && !HasVisibleNotes;

    public bool IsTreeViewMode
    {
        get => isTreeViewMode;
        set
        {
            if (SetProperty(ref isTreeViewMode, value))
            {
                OnPropertyChanged(nameof(IsListViewMode));
                OnPropertyChanged(nameof(ViewModeLabel));
            }
        }
    }

    public bool IsListViewMode => !IsTreeViewMode;

    public string ViewModeLabel => IsTreeViewMode ? "Tree view" : "Data grid";

    public string NoteCountLabel => $"{allNotes.Count} note{(allNotes.Count == 1 ? string.Empty : "s")}";

    public string ResultsSummary => HasVisibleNotes
        ? $"Showing {VisibleNotes.Count} of {allNotes.Count} notes"
        : $"0 of {allNotes.Count} notes";

    public string TypeFilterLabel
    {
        get => typeFilterLabel;
        private set => SetProperty(ref typeFilterLabel, value);
    }

    public string TagFilterLabel
    {
        get => tagFilterLabel;
        private set => SetProperty(ref tagFilterLabel, value);
    }

    public bool HasSearchQuery
    {
        get => hasSearchQuery;
        private set => SetProperty(ref hasSearchQuery, value);
    }

    public bool HasSelectedTypeFilters
    {
        get => hasSelectedTypeFilters;
        private set => SetProperty(ref hasSelectedTypeFilters, value);
    }

    public bool HasSelectedTagFilters
    {
        get => hasSelectedTagFilters;
        private set => SetProperty(ref hasSelectedTagFilters, value);
    }

    public bool HasActiveFilters
    {
        get => hasActiveFilters;
        private set => SetProperty(ref hasActiveFilters, value);
    }

    public IReadOnlyList<AllNotesFilterOptionViewModel> ActiveTypeFilters
    {
        get => activeTypeFilters;
        private set => SetProperty(ref activeTypeFilters, value);
    }

    public IReadOnlyList<AllNotesFilterOptionViewModel> ActiveTagFilters
    {
        get => activeTagFilters;
        private set => SetProperty(ref activeTagFilters, value);
    }

    public string CollectionLabel => string.IsNullOrWhiteSpace(currentCollectionId)
        ? "No collection selected"
        : Path.GetFileName(currentCollectionId.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public string PageSubtitle => $"{CollectionLabel} · {NoteCountLabel}";

    public AllNotesFilterOptionViewModel? SelectedTagOption
    {
        get => selectedTagOption;
        set
        {
            if (SetProperty(ref selectedTagOption, value))
            {
                QueueApplyFilters();
            }
        }
    }

    public AllNotesFilterOptionViewModel? SelectedTypeOption
    {
        get => selectedTypeOption;
        set
        {
            if (SetProperty(ref selectedTypeOption, value))
            {
                QueueApplyFilters();
            }
        }
    }

    public AllNotesSortOptionViewModel? SelectedSortOption
    {
        get => selectedSortOption;
        set
        {
            if (SetProperty(ref selectedSortOption, value))
            {
                QueueApplyFilters();
            }
        }
    }

    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectOnlyAllFilterOption(TagOptions, AllTagsValue);
        SelectOnlyAllFilterOption(TypeOptions, AllTypesValue);
        SelectedSortOption = SortOptions.FirstOrDefault();
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private bool CanOpenNoteFromCommand(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(currentCollectionId))
        {
            return false;
        }

        return parameter switch
        {
            AllNotesListItemViewModel note => !string.IsNullOrWhiteSpace(note.RelativePath),
            AllNotesTreeItemViewModel treeItem => treeItem.CanOpen,
            _ => false
        };
    }

    private void OpenNoteFromCommand(object? parameter)
    {
        string? relativePath = parameter switch
        {
            AllNotesListItemViewModel note => note.RelativePath,
            AllNotesTreeItemViewModel treeItem when treeItem.CanOpen => treeItem.RelativePath,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            OpenNoteRequested?.Invoke(this, new OpenAllNotesRequestedEventArgs(relativePath));
        }
    }

    private bool CanShowCardsFromCommand(object? parameter)
    {
        return parameter switch
        {
            AllNotesListItemViewModel note => note.HasCards,
            AllNotesTreeItemViewModel treeItem => treeItem.HasCards,
            _ => false
        };
    }

    private void ShowCardsFromCommand(object? parameter)
    {
        (string? relativePath, string? title) = parameter switch
        {
            AllNotesListItemViewModel note when note.HasCards => (note.RelativePath, note.Title),
            AllNotesTreeItemViewModel treeItem when treeItem.HasCards => (treeItem.RelativePath, treeItem.Title),
            _ => (null, null)
        };

        if (!string.IsNullOrWhiteSpace(relativePath) && !string.IsNullOrWhiteSpace(title))
        {
            ShowCardsRequested?.Invoke(this, new OpenNoteCardsRequestedEventArgs(relativePath, title));
        }
    }

    internal void RequestBulkTagEdit(
        IReadOnlyList<long> itemIds,
        KnowledgeTagOperationKind operation)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        TagEditRequested?.Invoke(
            this,
            new TagEditRequestedEventArgs(
                itemIds,
                operation,
                itemIds.Count == 1 ? "1 item" : $"{itemIds.Count} selected items"));
    }

    private bool CanRequestTagEdit(object? parameter)
    {
        return GetTagTarget(parameter) is { Id: > 0 };
    }

    private void RequestTagEdit(object? parameter, KnowledgeTagOperationKind operation)
    {
        (long id, string title) = GetTagTarget(parameter) ?? default;
        if (id <= 0)
        {
            return;
        }

        TagEditRequested?.Invoke(
            this,
            new TagEditRequestedEventArgs([id], operation, title));
    }

    private static (long Id, string Title)? GetTagTarget(object? parameter)
    {
        return parameter switch
        {
            AllNotesListItemViewModel note => (note.ItemId, note.Title),
            AllNotesTreeItemViewModel treeItem => (treeItem.Id, treeItem.Title),
            _ => null
        };
    }

    private void AddFilterOption(
        ObservableCollection<AllNotesFilterOptionViewModel> collection,
        string allValue,
        AllNotesFilterOptionViewModel option)
    {
        option.PropertyChanged += FilterOption_PropertyChanged;
        filterOptionMetadata[option] = (collection, allValue);
        collection.Add(option);
    }

    private void FilterOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isUpdatingFilterOptions || e.PropertyName != nameof(AllNotesFilterOptionViewModel.IsSelected))
        {
            return;
        }

        if (sender is not AllNotesFilterOptionViewModel option)
        {
            return;
        }

        if (!filterOptionMetadata.TryGetValue(option, out (ObservableCollection<AllNotesFilterOptionViewModel> Owner, string AllValue) metadata))
        {
            return;
        }

        NormalizeFilterSelection(metadata.Owner, metadata.AllValue, option);
        RefreshFilterPresentationState();
        QueueApplyFilters();
    }

    private void NormalizeFilterSelection(
        ObservableCollection<AllNotesFilterOptionViewModel> options,
        string allValue,
        AllNotesFilterOptionViewModel changedOption)
    {
        isUpdatingFilterOptions = true;
        try
        {
            if (string.Equals(changedOption.Value, allValue, StringComparison.Ordinal))
            {
                if (changedOption.IsSelected)
                {
                    ClearOtherFilterOptions(options, changedOption);
                    ClearActiveFilterState(allValue);
                }
                else if (!HasActiveFilterSelections(allValue))
                {
                    changedOption.IsSelected = true;
                }

                return;
            }

            AllNotesFilterOptionViewModel? allOption = GetAllFilterOption(options, allValue);
            if (changedOption.IsSelected)
            {
                AddActiveFilterOption(changedOption, allValue);
                if (allOption is not null)
                {
                    allOption.IsSelected = false;
                }
            }
            else
            {
                RemoveActiveFilterOption(changedOption, allValue);
                if (!HasActiveFilterSelections(allValue) && allOption is not null)
                {
                    allOption.IsSelected = true;
                }
            }
        }
        finally
        {
            isUpdatingFilterOptions = false;
        }
    }
    private void SelectOnlyAllFilterOption(
        ObservableCollection<AllNotesFilterOptionViewModel> options,
        string allValue)
    {
        isUpdatingFilterOptions = true;
        try
        {
            foreach (AllNotesFilterOptionViewModel option in options)
            {
                option.IsSelected = string.Equals(option.Value, allValue, StringComparison.Ordinal);
            }

            ClearActiveFilterState(allValue);
        }
        finally
        {
            isUpdatingFilterOptions = false;
        }

        RefreshFilterPresentationState();
        QueueApplyFilters();
    }
    private void RefreshFilterPresentationState()
    {
        int activeTypeCount = ActiveTypeFilters.Count;
        int activeTagCount = ActiveTagFilters.Count;

        HasSelectedTypeFilters = activeTypeCount > 0;
        HasSelectedTagFilters = activeTagCount > 0;
        TypeFilterLabel = CreateFilterButtonLabel(activeTypeCount, "Any type", "1 type", "types");
        TagFilterLabel = CreateFilterButtonLabel(activeTagCount, "Any tag", "1 tag", "tags");
        UpdateSearchFilterState();
    }
    private void UpdateSearchFilterState()
    {
        HasSearchQuery = !string.IsNullOrWhiteSpace(searchQuery);
        HasActiveFilters = HasSearchQuery || HasSelectedTypeFilters || HasSelectedTagFilters;
    }

    private static string CreateFilterButtonLabel(
        int selectedCount,
        string emptyLabel,
        string singularLabel,
        string pluralSuffix)
    {
        return selectedCount switch
        {
            0 => emptyLabel,
            1 => singularLabel,
            _ => $"{selectedCount} {pluralSuffix}"
        };
    }

    private void ClearActiveFilterState(string allValue)
    {
        if (string.Equals(allValue, AllTagsValue, StringComparison.Ordinal))
        {
            selectedTagFilterSet.Clear();
            ActiveTagFilters = [];
            return;
        }

        selectedTypeFilterSet.Clear();
        ActiveTypeFilters = [];
    }

    private bool HasActiveFilterSelections(string allValue)
    {
        return string.Equals(allValue, AllTagsValue, StringComparison.Ordinal)
            ? selectedTagFilterSet.Count > 0
            : selectedTypeFilterSet.Count > 0;
    }

    private void AddActiveFilterOption(AllNotesFilterOptionViewModel option, string allValue)
    {
        if (string.Equals(allValue, AllTagsValue, StringComparison.Ordinal))
        {
            if (selectedTagFilterSet.Add(option.Value))
            {
                ActiveTagFilters = AppendActiveFilterOptionEntry(ActiveTagFilters, option);
            }

            return;
        }

        if (Enum.TryParse(option.Value, ignoreCase: true, out KnowledgeNoteListItemType noteType)
            && selectedTypeFilterSet.Add(noteType))
        {
            ActiveTypeFilters = AppendActiveFilterOptionEntry(ActiveTypeFilters, option);
        }
    }

    private void RemoveActiveFilterOption(AllNotesFilterOptionViewModel option, string allValue)
    {
        if (string.Equals(allValue, AllTagsValue, StringComparison.Ordinal))
        {
            if (selectedTagFilterSet.Remove(option.Value))
            {
                ActiveTagFilters = RemoveActiveFilterOptionEntry(ActiveTagFilters, option);
            }

            return;
        }

        if (Enum.TryParse(option.Value, ignoreCase: true, out KnowledgeNoteListItemType noteType)
            && selectedTypeFilterSet.Remove(noteType))
        {
            ActiveTypeFilters = RemoveActiveFilterOptionEntry(ActiveTypeFilters, option);
        }
    }

    private void SyncTagFilterStateFromOptions()
    {
        selectedTagFilterSet.Clear();

        List<AllNotesFilterOptionViewModel> activeOptions = [];
        foreach (AllNotesFilterOptionViewModel option in TagOptions)
        {
            if (option.IsSelected && !string.Equals(option.Value, AllTagsValue, StringComparison.Ordinal))
            {
                selectedTagFilterSet.Add(option.Value);
                activeOptions.Add(option);
            }
        }

        ActiveTagFilters = activeOptions.Count == 0 ? [] : activeOptions.ToArray();
    }

    private void SyncTypeFilterStateFromOptions()
    {
        selectedTypeFilterSet.Clear();

        List<AllNotesFilterOptionViewModel> activeOptions = [];
        foreach (AllNotesFilterOptionViewModel option in TypeOptions)
        {
            if (!option.IsSelected || string.Equals(option.Value, AllTypesValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (Enum.TryParse(option.Value, ignoreCase: true, out KnowledgeNoteListItemType noteType))
            {
                selectedTypeFilterSet.Add(noteType);
                activeOptions.Add(option);
            }
        }

        ActiveTypeFilters = activeOptions.Count == 0 ? [] : activeOptions.ToArray();
    }

    private HashSet<string> CreateSelectedTagFilterSnapshot()
    {
        return new HashSet<string>(selectedTagFilterSet, StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<KnowledgeNoteListItemType> CreateSelectedTypeFilterSnapshot()
    {
        return new HashSet<KnowledgeNoteListItemType>(selectedTypeFilterSet);
    }

    private static IReadOnlyList<AllNotesFilterOptionViewModel> AppendActiveFilterOptionEntry(
        IReadOnlyList<AllNotesFilterOptionViewModel> options,
        AllNotesFilterOptionViewModel option)
    {
        if (options.Count == 0)
        {
            return [option];
        }

        AllNotesFilterOptionViewModel[] nextOptions = new AllNotesFilterOptionViewModel[options.Count + 1];
        for (int index = 0; index < options.Count; index++)
        {
            nextOptions[index] = options[index];
        }

        nextOptions[^1] = option;
        return nextOptions;
    }

    private static IReadOnlyList<AllNotesFilterOptionViewModel> RemoveActiveFilterOptionEntry(
        IReadOnlyList<AllNotesFilterOptionViewModel> options,
        AllNotesFilterOptionViewModel option)
    {
        if (options.Count == 0)
        {
            return [];
        }

        int foundIndex = -1;
        for (int index = 0; index < options.Count; index++)
        {
            if (ReferenceEquals(options[index], option))
            {
                foundIndex = index;
                break;
            }
        }

        if (foundIndex < 0)
        {
            return options;
        }

        if (options.Count == 1)
        {
            return [];
        }

        AllNotesFilterOptionViewModel[] nextOptions = new AllNotesFilterOptionViewModel[options.Count - 1];
        for (int sourceIndex = 0, targetIndex = 0; sourceIndex < options.Count; sourceIndex++)
        {
            if (sourceIndex == foundIndex)
            {
                continue;
            }

            nextOptions[targetIndex++] = options[sourceIndex];
        }

        return nextOptions;
    }

    private static void EnsureAllOptionSelectedWhenNoConcreteSelection(
        ObservableCollection<AllNotesFilterOptionViewModel> options,
        string allValue)
    {
        if (HasSelectedConcreteFilterOption(options, allValue))
        {
            return;
        }

        AllNotesFilterOptionViewModel? allOption = GetAllFilterOption(options, allValue);
        if (allOption is not null)
        {
            allOption.IsSelected = true;
        }
    }

    private static void ClearOtherFilterOptions(
        IEnumerable<AllNotesFilterOptionViewModel> options,
        AllNotesFilterOptionViewModel selectedOption)
    {
        foreach (AllNotesFilterOptionViewModel option in options)
        {
            if (!ReferenceEquals(option, selectedOption))
            {
                option.IsSelected = false;
            }
        }
    }

    private static bool HasSelectedConcreteFilterOption(
        IEnumerable<AllNotesFilterOptionViewModel> options,
        string allValue)
    {
        foreach (AllNotesFilterOptionViewModel option in options)
        {
            if (option.IsSelected && !string.Equals(option.Value, allValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static AllNotesFilterOptionViewModel? GetAllFilterOption(
        ObservableCollection<AllNotesFilterOptionViewModel> options,
        string allValue)
    {
        if (options.Count > 0 && string.Equals(options[0].Value, allValue, StringComparison.Ordinal))
        {
            return options[0];
        }

        foreach (AllNotesFilterOptionViewModel option in options)
        {
            if (string.Equals(option.Value, allValue, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }
}
