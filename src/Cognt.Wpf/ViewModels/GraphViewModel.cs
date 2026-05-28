using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using PkmNoteService = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.INoteService;

namespace CogntApp.ViewModels;

public sealed class GraphViewModel : ObservableObject
{
    private readonly PkmNoteService noteService;
    private readonly MainViewModel mainViewModel;
    private KnowledgeGraphReadModel graph = KnowledgeGraphReadModel.Empty(KnowledgeGraphSettingsReadModel.Default);
    private IReadOnlyList<KnowledgeGraphNodeReadModel> orderedGraphSearchNodes = Array.Empty<KnowledgeGraphNodeReadModel>();
    private bool isLoading;
    private string errorMessage = string.Empty;
    private string statusMessage = string.Empty;
    private int neighborDepth = KnowledgeGraphSettingsReadModel.DefaultNeighborDepth;
    private string neighborDepthText = KnowledgeGraphSettingsReadModel.DefaultNeighborDepth.ToString(CultureInfo.InvariantCulture);
    private string maxNodesText = KnowledgeGraphSettingsReadModel.DefaultMaxNodes.ToString(CultureInfo.InvariantCulture);
    private string searchText = string.Empty;
    private KnowledgeGraphNodeReadModel? selectedSearchResult;
    private string currentNodeTitle = "Graph";
    private string focusedDocumentId = string.Empty;
    private string lastGraphCollectionPath = string.Empty;
    private string lastGraphRelativePath = string.Empty;
    private bool isRefreshingForCurrentNote;
    private int graphRefreshVersion;
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public GraphViewModel(PkmNoteService noteService, MainViewModel mainViewModel)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(mainViewModel);

        this.noteService = noteService;
        this.mainViewModel = mainViewModel;

        RefreshCommand = new RelayCommand(RefreshGraph);
        SaveSettingsCommand = new RelayCommand(SaveSettingsAndRefresh);
        OpenNodeCommand = new RelayCommand(OpenNode);
        FocusSearchResultCommand = new RelayCommand(_ => FocusSearchResult());

        mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
    }

    public ICommand RefreshCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand OpenNodeCommand { get; }

    public ICommand FocusSearchResultCommand { get; }

    public ObservableCollection<KnowledgeGraphNodeReadModel> SearchResults { get; } = [];

    public KnowledgeGraphReadModel Graph
    {
        get => graph;
        private set
        {
            if (SetProperty(ref graph, value))
            {
                orderedGraphSearchNodes = CreateOrderedGraphSearchNodes(value);
                CurrentNodeTitle = ResolveCurrentNodeTitle(value);
                OnPropertyChanged(nameof(HasGraph));
                OnPropertyChanged(nameof(ShouldShowState));
                OnPropertyChanged(nameof(GraphSummary));
                FocusedDocumentId = value.CurrentDocumentId ?? string.Empty;
                RebuildSearchResults();
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
                OnPropertyChanged(nameof(ShouldShowState));
            }
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShouldShowState));
                OnPropertyChanged(nameof(StateTitle));
                OnPropertyChanged(nameof(StateMessage));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public int NeighborDepth
    {
        get => neighborDepth;
        set
        {
            int normalized = Math.Clamp(
                value,
                KnowledgeGraphSettingsReadModel.MinimumNeighborDepth,
                KnowledgeGraphSettingsReadModel.MaximumNeighborDepth);
            if (SetProperty(ref neighborDepth, normalized))
            {
                StatusMessage = string.Empty;
                neighborDepthText = normalized.ToString(CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(NeighborDepthText));
                OnPropertyChanged(nameof(NeighborDepthDisplay));
            }
        }
    }

    public string NeighborDepthDisplay =>
        NeighborDepth == 1
            ? "1 hop"
            : $"{NeighborDepth} hops";

    public string NeighborDepthText
    {
        get => neighborDepthText;
        set
        {
            if (!SetProperty(ref neighborDepthText, value))
            {
                return;
            }

            StatusMessage = string.Empty;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                int normalized = Math.Clamp(
                    parsed,
                    KnowledgeGraphSettingsReadModel.MinimumNeighborDepth,
                    KnowledgeGraphSettingsReadModel.MaximumNeighborDepth);
                if (neighborDepth != normalized)
                {
                    neighborDepth = normalized;
                    OnPropertyChanged(nameof(NeighborDepth));
                    OnPropertyChanged(nameof(NeighborDepthDisplay));
                }
            }
        }
    }

    public string MaxNodesText
    {
        get => maxNodesText;
        set
        {
            if (SetProperty(ref maxNodesText, value))
            {
                StatusMessage = string.Empty;
            }
        }
    }

    public bool HasGraph => Graph.Nodes.Count > 1 || Graph.Edges.Count > 0;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                RebuildSearchResults();
            }
        }
    }

    public KnowledgeGraphNodeReadModel? SelectedSearchResult
    {
        get => selectedSearchResult;
        set
        {
            if (SetProperty(ref selectedSearchResult, value) && value is not null)
            {
                FocusNode(value);
            }
        }
    }

    public string FocusedDocumentId
    {
        get => focusedDocumentId;
        private set => SetProperty(ref focusedDocumentId, value);
    }

    public string CurrentNodeTitle
    {
        get => currentNodeTitle;
        private set => SetProperty(ref currentNodeTitle, value);
    }

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool ShouldShowSearchResults =>
        !string.IsNullOrWhiteSpace(SearchText)
        && HasSearchResults;

    public string SearchStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return string.Empty;
            }

            return HasSearchResults
                ? CountDisplayFormatter.Format(SearchResults.Count, "match")
                : "No matching node";
        }
    }

    public bool ShouldShowState => IsLoading || HasError || !HasGraph;

    public string StateTitle
    {
        get
        {
            if (IsLoading)
            {
                return "Loading graph";
            }

            if (HasError)
            {
                return "Graph unavailable";
            }

            if (!mainViewModel.HasOpenCollection)
            {
                return "No collection open";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "No note selected"
                : "No links found";
        }
    }

    public string StateMessage
    {
        get
        {
            if (IsLoading)
            {
                return "Reading inbound and outgoing note links.";
            }

            if (HasError)
            {
                return ErrorMessage;
            }

            if (!mainViewModel.HasOpenCollection)
            {
                return "Select a collection to inspect its note graph.";
            }

            return string.IsNullOrWhiteSpace(mainViewModel.CurrentNoteRelativePath)
                ? "Open a note to center the graph."
                : "This note has no resolved links within the selected range.";
        }
    }

    public string GraphSummary
    {
        get
        {
            if (!HasGraph)
            {
                return string.Empty;
            }

            string shown = $"{Graph.Nodes.Count} shown";
            string reachable = Graph.IsTruncated
                ? $" of {Graph.TotalReachableNodeCount} reachable"
                : string.Empty;
            string links = CountDisplayFormatter.Format(Graph.Edges.Count, "link");
            return $"{shown}{reachable} · {links}";
        }
    }

    internal Task LoadAsync()
    {
        LoadSettings();
        RefreshGraph();
        return Task.CompletedTask;
    }

    private void LoadSettings()
    {
        if (!mainViewModel.HasOpenCollection)
        {
            ApplySettings(KnowledgeGraphSettingsReadModel.Default);
            return;
        }

        ApplySettings(noteService.LoadGraphViewSettings(mainViewModel.OpenCollectionPath));
    }

    private void RefreshGraph()
    {
        int refreshVersion = ++graphRefreshVersion;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        if (!TryCreateSettings(out KnowledgeGraphSettingsReadModel settings))
        {
            IsLoading = false;
            return;
        }

        if (!mainViewModel.HasOpenCollection)
        {
            Graph = KnowledgeGraphReadModel.Empty(settings);
            lastGraphCollectionPath = mainViewModel.OpenCollectionPath ?? string.Empty;
            lastGraphRelativePath = string.Empty;
            IsLoading = false;
            OnPropertyChanged(nameof(StateTitle));
            OnPropertyChanged(nameof(StateMessage));
            return;
        }

        string? currentRelativePath = mainViewModel.CurrentNoteRelativePath;
        if (string.IsNullOrWhiteSpace(currentRelativePath))
        {
            Graph = KnowledgeGraphReadModel.Empty(settings);
            lastGraphCollectionPath = mainViewModel.OpenCollectionPath;
            lastGraphRelativePath = string.Empty;
            IsLoading = false;
            OnPropertyChanged(nameof(StateTitle));
            OnPropertyChanged(nameof(StateMessage));
            return;
        }

        try
        {
            IsLoading = true;
            mainViewModel.SaveCurrentNoteFromEditor();
            string currentContent = mainViewModel.GetCurrentEditorContent?.Invoke() ?? string.Empty;
            string collectionPath = mainViewModel.OpenCollectionPath;
            _ = Task.Run(() => noteService.ReadDocumentGraph(
                    collectionPath,
                    currentRelativePath,
                    currentContent,
                    settings))
                .ContinueWith(
                    task => ApplyGraphRefreshResult(
                        refreshVersion,
                        collectionPath,
                        currentRelativePath,
                        settings,
                        task),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            Graph = KnowledgeGraphReadModel.Empty(settings);
            ErrorMessage = ex.Message;
            IsLoading = false;
            OnPropertyChanged(nameof(StateTitle));
            OnPropertyChanged(nameof(StateMessage));
        }
    }

    private void ApplyGraphRefreshResult(
        int refreshVersion,
        string collectionPath,
        string relativePath,
        KnowledgeGraphSettingsReadModel settings,
        Task<KnowledgeGraphReadModel> task)
    {
        void ApplyIfCurrent()
        {
            if (refreshVersion != graphRefreshVersion)
            {
                return;
            }

            try
            {
                if (task.IsCompletedSuccessfully)
                {
                    Graph = task.Result;
                    lastGraphCollectionPath = collectionPath;
                    lastGraphRelativePath = relativePath;
                }
                else
                {
                    Graph = KnowledgeGraphReadModel.Empty(settings);
                    ErrorMessage = task.Exception?.GetBaseException().Message ?? "The graph could not be loaded.";
                }
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(StateTitle));
                OnPropertyChanged(nameof(StateMessage));
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ApplyIfCurrent);
            return;
        }

        ApplyIfCurrent();
    }

    private void SaveSettingsAndRefresh()
    {
        if (!TryCreateSettings(out KnowledgeGraphSettingsReadModel settings))
        {
            return;
        }

        if (!mainViewModel.HasOpenCollection)
        {
            ErrorMessage = "Select a collection to save graph settings.";
            return;
        }

        try
        {
            noteService.SaveGraphViewSettings(mainViewModel.OpenCollectionPath, settings);
            RefreshGraph();
            StatusMessage = "Graph settings saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private bool TryCreateSettings(out KnowledgeGraphSettingsReadModel settings)
    {
        if (!int.TryParse(NeighborDepthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int neighborDepthValue))
        {
            settings = KnowledgeGraphSettingsReadModel.Default;
            ErrorMessage = "Depth must be a whole number from 1 to 4.";
            return false;
        }

        if (!int.TryParse(MaxNodesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxNodes))
        {
            settings = KnowledgeGraphSettingsReadModel.Default;
            ErrorMessage = "Max nodes must be a whole number.";
            return false;
        }

        settings = new KnowledgeGraphSettingsReadModel(neighborDepthValue, maxNodes).Normalize();
        NeighborDepth = settings.NeighborDepth;
        MaxNodesText = settings.MaxNodes.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private void ApplySettings(KnowledgeGraphSettingsReadModel settings)
    {
        KnowledgeGraphSettingsReadModel normalized = settings.Normalize();
        NeighborDepth = normalized.NeighborDepth;
        MaxNodesText = normalized.MaxNodes.ToString(CultureInfo.InvariantCulture);
    }

    internal string CreatePreviewText(KnowledgeGraphNodeReadModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        try
        {
            string preview = mainViewModel.ReadMentionPreview(node.RelativePath, 260);
            preview = WhitespaceRegex.Replace(preview ?? string.Empty, " ").Trim();
            if (preview.Length > 260)
            {
                preview = $"{preview[..257]}...";
            }

            return string.IsNullOrWhiteSpace(preview)
                ? node.Title
                : $"{node.Title}{Environment.NewLine}{preview}";
        }
        catch
        {
            return string.IsNullOrWhiteSpace(node.Title)
                ? node.RelativePath
                : node.Title;
        }
    }

    private void OpenNode(object? parameter)
    {
        try
        {
            string? relativePath = parameter switch
            {
                KnowledgeGraphNodeReadModel node => node.RelativePath,
                string path => path,
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            mainViewModel.OpenNoteByRelativePath(relativePath);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void FocusSearchResult()
    {
        KnowledgeGraphNodeReadModel? node = SelectedSearchResult
            ?? (SearchResults.Count > 0 ? SearchResults[0] : null);
        if (node is null)
        {
            StatusMessage = string.IsNullOrWhiteSpace(SearchText)
                ? string.Empty
                : "No graph node found.";
            return;
        }

        FocusNode(node);
    }

    private void FocusNode(KnowledgeGraphNodeReadModel node)
    {
        FocusedDocumentId = node.DocumentId;
        StatusMessage = $"Focused {node.Title}.";
    }

    private static string ResolveCurrentNodeTitle(KnowledgeGraphReadModel graph)
    {
        foreach (KnowledgeGraphNodeReadModel node in graph.Nodes)
        {
            if (node.IsCurrent)
            {
                return node.Title;
            }
        }

        return "Graph";
    }

    private void RebuildSearchResults()
    {
        string query = SearchText.Trim();
        (KnowledgeGraphNodeReadModel[] results, bool containsSelectedResult) = CreateSearchResults(query);

        ApplySearchResults(results);

        if (selectedSearchResult is not null && !containsSelectedResult)
        {
            selectedSearchResult = null;
            OnPropertyChanged(nameof(SelectedSearchResult));
        }

        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShouldShowSearchResults));
        OnPropertyChanged(nameof(SearchStatus));
    }

    private (KnowledgeGraphNodeReadModel[] Results, bool ContainsSelectedResult) CreateSearchResults(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (Array.Empty<KnowledgeGraphNodeReadModel>(), false);
        }

        string? selectedDocumentId = selectedSearchResult?.DocumentId;
        bool containsSelectedResult = false;
        List<KnowledgeGraphNodeReadModel> matches = [];

        foreach (KnowledgeGraphNodeReadModel node in orderedGraphSearchNodes)
        {
            if (!node.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !node.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(node);
            if (!containsSelectedResult
                && selectedDocumentId is not null
                && string.Equals(node.DocumentId, selectedDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                containsSelectedResult = true;
            }

            if (matches.Count >= 12)
            {
                break;
            }
        }

        return (matches.Count == 0 ? Array.Empty<KnowledgeGraphNodeReadModel>() : [.. matches], containsSelectedResult);
    }

    private void ApplySearchResults(IReadOnlyList<KnowledgeGraphNodeReadModel> nextResults)
    {
        int sharedCount = Math.Min(SearchResults.Count, nextResults.Count);
        for (int index = 0; index < sharedCount; index++)
        {
            if (!ReferenceEquals(SearchResults[index], nextResults[index]))
            {
                SearchResults[index] = nextResults[index];
            }
        }

        for (int index = SearchResults.Count - 1; index >= nextResults.Count; index--)
        {
            SearchResults.RemoveAt(index);
        }

        for (int index = sharedCount; index < nextResults.Count; index++)
        {
            SearchResults.Add(nextResults[index]);
        }
    }

    private static IReadOnlyList<KnowledgeGraphNodeReadModel> CreateOrderedGraphSearchNodes(KnowledgeGraphReadModel graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return Array.Empty<KnowledgeGraphNodeReadModel>();
        }

        List<KnowledgeGraphNodeReadModel> orderedNodes = [.. graph.Nodes];
        orderedNodes.Sort(CompareGraphSearchNodes);
        return orderedNodes;
    }

    private static int CompareGraphSearchNodes(
        KnowledgeGraphNodeReadModel left,
        KnowledgeGraphNodeReadModel right)
    {
        int depthComparison = left.Depth.CompareTo(right.Depth);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        int titleComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
        return titleComparison != 0
            ? titleComparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MainViewModel.CurrentNoteRelativePath)
            or nameof(MainViewModel.CurrentNotePath)
            or nameof(MainViewModel.OpenCollectionPath)))
        {
            return;
        }

        RefreshForCurrentNoteChange();
    }

    private void RefreshForCurrentNoteChange()
    {
        if (isRefreshingForCurrentNote)
        {
            return;
        }

        string collectionPath = mainViewModel.OpenCollectionPath ?? string.Empty;
        string relativePath = mainViewModel.CurrentNoteRelativePath ?? string.Empty;
        if (string.Equals(collectionPath, lastGraphCollectionPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(relativePath, lastGraphRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            isRefreshingForCurrentNote = true;
            SearchText = string.Empty;
            selectedSearchResult = null;
            OnPropertyChanged(nameof(SelectedSearchResult));
            LoadSettings();
            RefreshGraph();
        }
        finally
        {
            isRefreshingForCurrentNote = false;
        }
    }
}
