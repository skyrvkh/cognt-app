using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;
using PkmNoteService = Cognt.Modules.PersonalKnowledgeManagement.Application.Notes.INoteService;

namespace CogntApp.ViewModels;

public sealed partial class GraphViewModel : ObservableObject
{
    private readonly PkmNoteService noteService;
    private readonly MainViewModel mainViewModel;
    private readonly ILogger<GraphViewModel> logger;
    private readonly Dictionary<string, KnowledgeGraphReadModel> graphCache = new(StringComparer.OrdinalIgnoreCase);
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

    public GraphViewModel(PkmNoteService noteService, MainViewModel mainViewModel, ILogger<GraphViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(mainViewModel);
        ArgumentNullException.ThrowIfNull(logger);

        this.noteService = noteService;
        this.mainViewModel = mainViewModel;
        this.logger = logger;

        RefreshCommand = new RelayCommand(_ => RefreshGraph(force: true));
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
                orderedGraphSearchNodes = GraphSearchResultBuilder.CreateOrderedNodes(value);
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
        RefreshGraph(force: false);
        return Task.CompletedTask;
    }

    private void RefreshGraph(bool force)
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
            string currentContent = mainViewModel.GetCurrentEditorContent?.Invoke() ?? string.Empty;
            string collectionPath = mainViewModel.OpenCollectionPath;
            string cacheKey = CreateGraphCacheKey(collectionPath, currentRelativePath, settings, currentContent);
            if (!force && graphCache.TryGetValue(cacheKey, out KnowledgeGraphReadModel? cachedGraph))
            {
                Graph = cachedGraph;
                lastGraphCollectionPath = collectionPath;
                lastGraphRelativePath = currentRelativePath;
                IsLoading = false;
                OnPropertyChanged(nameof(StateTitle));
                OnPropertyChanged(nameof(StateMessage));
                return;
            }

            IsLoading = true;
            mainViewModel.SaveCurrentNoteFromEditor();
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
                        cacheKey,
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
            logger.LogError(
                ex,
                "Graph refresh failed before background read. RefreshVersion={RefreshVersion} RelativePath={RelativePath}",
                refreshVersion,
                currentRelativePath);
        }
    }

    private void ApplyGraphRefreshResult(
        int refreshVersion,
        string collectionPath,
        string relativePath,
        string cacheKey,
        KnowledgeGraphSettingsReadModel settings,
        Task<KnowledgeGraphReadModel> task)
    {
        Exception? failure = task.IsFaulted
            ? task.Exception?.GetBaseException()
            : null;

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
                    graphCache[cacheKey] = task.Result;
                    lastGraphCollectionPath = collectionPath;
                    lastGraphRelativePath = relativePath;
                }
                else
                {
                    Graph = KnowledgeGraphReadModel.Empty(settings);
                    ErrorMessage = CreateGraphRefreshErrorMessage(failure);
                    logger.LogError(
                        failure,
                        "Graph refresh background read failed. RefreshVersion={RefreshVersion} RelativePath={RelativePath} ErrorMessage={ErrorMessage}",
                        refreshVersion,
                        relativePath,
                        ErrorMessage);
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

    private static string CreateGraphRefreshErrorMessage(Exception? exception)
    {
        return exception switch
        {
            FileNotFoundException => "The selected note was deleted before the graph finished loading.",
            null => "The graph could not be loaded.",
            _ => exception.Message
        };
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
            RefreshGraph(force: true);
            StatusMessage = "Graph settings saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            logger.LogError(
                ex,
                "Graph settings save failed. CollectionPath={CollectionPath} NeighborDepth={NeighborDepth} MaxNodes={MaxNodes}",
                mainViewModel.OpenCollectionPath,
                settings.NeighborDepth,
                settings.MaxNodes);
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
            logger.LogError(ex, "Graph node open failed. ParameterType={ParameterType}", parameter?.GetType().Name ?? "<null>");
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
        GraphSearchResult result = GraphSearchResultBuilder.Build(
            orderedGraphSearchNodes,
            query,
            selectedSearchResult?.DocumentId);

        ApplySearchResults(result.Nodes);

        if (selectedSearchResult is not null && !result.ContainsSelectedResult)
        {
            selectedSearchResult = null;
            OnPropertyChanged(nameof(SelectedSearchResult));
        }

        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShouldShowSearchResults));
        OnPropertyChanged(nameof(SearchStatus));
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
            RefreshGraph(force: false);
        }
        finally
        {
            isRefreshingForCurrentNote = false;
        }
    }

    private static string CreateGraphCacheKey(
        string collectionPath,
        string relativePath,
        KnowledgeGraphSettingsReadModel settings,
        string content)
    {
        KnowledgeGraphSettingsReadModel normalized = settings.Normalize();
        return string.Join(
            "|",
            collectionPath.Trim(),
            relativePath.Trim(),
            normalized.NeighborDepth.ToString(CultureInfo.InvariantCulture),
            normalized.MaxNodes.ToString(CultureInfo.InvariantCulture),
            CreateContentHash(content));
    }

    private static string CreateContentHash(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(hash);
    }
}
