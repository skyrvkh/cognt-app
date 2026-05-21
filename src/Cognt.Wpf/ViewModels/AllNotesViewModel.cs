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
    private string currentCollectionId = string.Empty;
    private string searchQuery = string.Empty;
    private bool isLoading;
    private bool hasError;
    private string errorMessage = string.Empty;
    private string emptyStateTitle = "No notes yet.";
    private string emptyStateMessage = "Create or import a note to get started.";
    private bool hasLoadedCatalog;
    private AllNotesFilterOptionViewModel? selectedTagOption;
    private AllNotesFilterOptionViewModel? selectedTypeOption;
    private AllNotesSortOptionViewModel? selectedSortOption;
    private CancellationTokenSource? applyFiltersCancellation;
    private IReadOnlyList<KnowledgeNoteListItemReadModel> allNotes = [];
    private IReadOnlyList<AllNotesTreeItemViewModel> allTreeRoots = [];
    private Dictionary<string, int> cardCountsBySourceRelativePath = new(StringComparer.OrdinalIgnoreCase);
    private bool isTreeViewMode;
    private bool isUpdatingFilterOptions;

    public AllNotesViewModel(INoteService noteService, IFlashcardCardsQuery flashcardCardsQuery)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(flashcardCardsQuery);

        this.noteService = noteService;
        this.flashcardCardsQuery = flashcardCardsQuery;
        openNoteCommand = new RelayCommand(OpenNoteFromCommand, CanOpenNoteFromCommand);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        CreateNoteCommand = new RelayCommand(() => CreateNoteRequested?.Invoke(this, EventArgs.Empty));
        ImportDocumentsCommand = new RelayCommand(() => ImportDocumentsRequested?.Invoke(this, EventArgs.Empty));
        ShowDataGridViewCommand = new RelayCommand(() => IsTreeViewMode = false);
        ShowTreeViewCommand = new RelayCommand(() => IsTreeViewMode = true);

        AddFilterOption(TagOptions, new AllNotesFilterOptionViewModel("All tags", AllTagsValue, isSelected: true));
        AddFilterOption(TypeOptions, new AllNotesFilterOptionViewModel("All types", AllTypesValue, isSelected: true));

        foreach (AllNotesSortOptionViewModel option in CreateSortOptions())
        {
            SortOptions.Add(option);
        }

        selectedTagOption = TagOptions[0];
        selectedTypeOption = TypeOptions[0];
        selectedSortOption = SortOptions[0];
    }

    internal event EventHandler<OpenAllNotesRequestedEventArgs>? OpenNoteRequested;

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

    public ICommand ClearFiltersCommand { get; }

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

    private bool CanOpenNoteFromCommand(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(currentCollectionId))
        {
            return false;
        }

        return parameter switch
        {
            AllNotesListItemViewModel note => !string.IsNullOrWhiteSpace(note.RelativePath),
            AllNotesTreeItemViewModel => true,
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

    private void AddFilterOption(
        ObservableCollection<AllNotesFilterOptionViewModel> collection,
        AllNotesFilterOptionViewModel option)
    {
        option.PropertyChanged += FilterOption_PropertyChanged;
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

        ObservableCollection<AllNotesFilterOptionViewModel>? owner = TagOptions.Contains(option)
            ? TagOptions
            : TypeOptions.Contains(option)
                ? TypeOptions
                : null;

        if (owner is null)
        {
            return;
        }

        string allValue = ReferenceEquals(owner, TagOptions) ? AllTagsValue : AllTypesValue;
        NormalizeFilterSelection(owner, allValue, option);
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
                    foreach (AllNotesFilterOptionViewModel option in options.Where(option => option != changedOption))
                    {
                        option.IsSelected = false;
                    }
                }
                else if (!options.Any(option => option.IsSelected))
                {
                    changedOption.IsSelected = true;
                }

                return;
            }

            AllNotesFilterOptionViewModel? allOption = options
                .FirstOrDefault(option => string.Equals(option.Value, allValue, StringComparison.Ordinal));
            if (changedOption.IsSelected && allOption is not null)
            {
                allOption.IsSelected = false;
            }
            else if (!options.Any(option => option.IsSelected && !string.Equals(option.Value, allValue, StringComparison.Ordinal))
                && allOption is not null)
            {
                allOption.IsSelected = true;
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
        }
        finally
        {
            isUpdatingFilterOptions = false;
        }

        QueueApplyFilters();
    }
}
