using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CogntApp.ViewModels;

public sealed class NoteInspectorViewModel : ObservableObject
{
    private readonly MainViewModel mainViewModel;
    private bool isActive;

    public NoteInspectorViewModel(MainViewModel mainViewModel, GraphViewModel graphViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        ArgumentNullException.ThrowIfNull(graphViewModel);

        this.mainViewModel = mainViewModel;
        GraphViewModel = graphViewModel;

        RefreshCommand = new RelayCommand(Refresh);
        mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
    }

    public ObservableCollection<NoteOutlineItemViewModel> OutlineItems { get; } = [];

    public GraphViewModel GraphViewModel { get; }

    public ICommand RefreshCommand { get; }

    public bool HasOutline => OutlineItems.Count > 0;

    public bool ShouldShowOutlineState => !HasOutline;

    public string OutlineSummary => HasOutline
        ? CountDisplayFormatter.Format(OutlineItems.Count, "heading")
        : string.Empty;

    public string OutlineStateTitle
    {
        get
        {
            if (!mainViewModel.HasOpenCollection)
            {
                return "No collection open";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "No note selected"
                : "No headings";
        }
    }

    public string OutlineStateMessage
    {
        get
        {
            if (!mainViewModel.HasOpenCollection)
            {
                return "Select a collection to inspect note structure.";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "Open a note to show its outline."
                : "Add headings to this note to build an outline.";
        }
    }

    internal void Activate()
    {
        isActive = true;
        Refresh();
    }

    internal void Deactivate()
    {
        isActive = false;
    }

    internal void UpdateCurrentContent(string content)
    {
        LoadOutline(content);
        if (isActive)
        {
            RefreshGraph();
        }
    }

    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath))
        {
            LoadOutline(string.Empty);
        }
        else
        {
            LoadOutline(mainViewModel.GetCurrentEditorContent?.Invoke() ?? string.Empty);
        }

        _ = GraphViewModel.LoadAsync();
    }

    private void RefreshGraph()
    {
        if (GraphViewModel.RefreshCommand.CanExecute(null))
        {
            GraphViewModel.RefreshCommand.Execute(null);
        }
    }

    private void LoadOutline(string content)
    {
        OutlineItems.Clear();
        foreach (NoteOutlineItemViewModel item in NoteOutlineParser.Parse(content))
        {
            OutlineItems.Add(item);
        }

        NotifyOutlineChanged();
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentNoteRelativePath)
            or nameof(MainViewModel.CurrentNotePath)
            or nameof(MainViewModel.OpenCollectionPath))
        {
            NotifyOutlineChanged();
        }
    }

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(ShouldShowOutlineState));
        OnPropertyChanged(nameof(OutlineSummary));
        OnPropertyChanged(nameof(OutlineStateTitle));
        OnPropertyChanged(nameof(OutlineStateMessage));
    }
}
