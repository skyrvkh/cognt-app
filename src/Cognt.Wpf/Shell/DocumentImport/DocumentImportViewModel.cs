using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel : ObservableObject
{
    private readonly MainViewModel target;
    private readonly ILogger<DocumentImportViewModel>? logger;
    private bool isImporting;
    private bool openImportedDocumentAfterImport = true;
    private string message = string.Empty;
    private bool isMessageError;
    private DocumentImportQueueSnapshot queueState = DocumentImportQueueSnapshot.Empty;
    private string queueSummary = DocumentImportQueueSnapshot.Empty.QueueSummary;
    private bool canImport;

    internal DocumentImportViewModel(
        MainViewModel target,
        ILogger<DocumentImportViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        this.target = target;
        this.logger = logger;
        RemoveFileCommand = new RelayCommand(RemoveFile, parameter => parameter is DocumentImportQueueItem { CanRemove: true });
        ImportCommand = new RelayCommand(_ => { _ = ImportAsync(); }, _ => CanImport);
    }

    public ObservableCollection<DocumentImportQueueItem> Files { get; } = [];

    public ICommand RemoveFileCommand { get; }

    public ICommand ImportCommand { get; }

    public string DestinationLabel => string.IsNullOrWhiteSpace(target.CurrentCollectionName)
        ? "Current workspace"
        : target.CurrentCollectionName;

    public bool IsImporting
    {
        get => isImporting;
        private set
        {
            if (SetProperty(ref isImporting, value))
            {
                RefreshDerivedState();
            }
        }
    }

    public bool OpenImportedDocumentAfterImport
    {
        get => openImportedDocumentAfterImport;
        set => SetProperty(ref openImportedDocumentAfterImport, value);
    }

    public string Message
    {
        get => message;
        private set
        {
            if (SetProperty(ref message, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsMessageError
    {
        get => isMessageError;
        private set => SetProperty(ref isMessageError, value);
    }

    public bool HasFiles => Files.Count > 0;

    public bool HasNoFiles => !HasFiles;

    public string QueueSummary => queueSummary;

    public bool CanImport => canImport;

    public bool CanImportCollection => !IsImporting;

    public string ImportButtonText => IsImporting ? "Adding..." : "Add to collection";

    internal string DefaultCollectionParentPath => target.GetDefaultCollectionCreationParentPath();

    internal void AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string[] pathSnapshot = paths.ToArray();
        List<string> rejected = [];
        HashSet<string> existingPaths = CreateExistingFullPathSet();

        foreach (string path in pathSnapshot)
        {
            QueueImportPath(path, existingPaths, rejected);
        }

        UpdateMessageState(
            rejected.Count == 0 ? string.Empty : string.Join(Environment.NewLine, rejected),
            isError: rejected.Count > 0);
        RefreshDerivedState();
    }

    private void RefreshDerivedState()
    {
        queueState = DocumentImportQueueSnapshot.Create(Files, IsImporting);
        queueSummary = queueState.QueueSummary;
        canImport = queueState.CanImport;

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasNoFiles));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanImportCollection));
        OnPropertyChanged(nameof(ImportButtonText));
        if (ImportCommand is RelayCommand importCommand)
        {
            importCommand.RaiseCanExecuteChanged();
        }

        if (RemoveFileCommand is RelayCommand removeCommand)
        {
            removeCommand.RaiseCanExecuteChanged();
        }
    }

    private void UpdateMessageState(string value, bool isError)
    {
        IsMessageError = isError;
        Message = value;
    }

    private HashSet<string> CreateExistingFullPathSet()
    {
        HashSet<string> existingPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (DocumentImportQueueItem file in Files)
        {
            existingPaths.Add(Path.GetFullPath(file.FullPath));
        }

        return existingPaths;
    }

}
