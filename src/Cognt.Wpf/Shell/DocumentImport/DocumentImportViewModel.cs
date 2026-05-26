using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CogntApp.ViewModels;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel : ObservableObject
{
    private readonly MainViewModel target;
    private bool isImporting;
    private bool openImportedDocumentAfterImport = true;
    private string message = string.Empty;
    private bool isMessageError;
    private string queueSummary = "No files selected";
    private bool canImport;
    private int importableFileCount;
    private int importedFileCount;
    private int failedFileCount;

    internal DocumentImportViewModel(MainViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        this.target = target;
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

    public string ImportButtonText => IsImporting ? "Importing..." : "Import";

    internal void AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        List<string> rejected = [];
        HashSet<string> existingPaths = CreateExistingFullPathSet();

        foreach (string path in paths)
        {
            string addError = TryQueueFile(path, existingPaths);
            if (!string.IsNullOrWhiteSpace(addError))
            {
                rejected.Add(addError);
            }
        }

        UpdateMessageState(
            rejected.Count == 0 ? string.Empty : string.Join(Environment.NewLine, rejected),
            isError: rejected.Count > 0);
        RefreshDerivedState();
    }

    private void RefreshDerivedState()
    {
        DocumentImportQueueState state = CreateQueueState(Files, IsImporting);
        queueSummary = state.QueueSummary;
        canImport = state.CanImport;
        importableFileCount = state.ImportableFileCount;
        importedFileCount = state.ImportedFileCount;
        failedFileCount = state.FailedFileCount;

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasNoFiles));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(CanImport));
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

    private static DocumentImportQueueState CreateQueueState(
        IReadOnlyCollection<DocumentImportQueueItem> files,
        bool isImporting)
    {
        int importableCount = 0;
        int importedCount = 0;
        int failedCount = 0;

        foreach (DocumentImportQueueItem file in files)
        {
            switch (file.Status)
            {
                case DocumentImportStatus.Ready:
                    importableCount++;
                    break;
                case DocumentImportStatus.Failed:
                    importableCount++;
                    failedCount++;
                    break;
                case DocumentImportStatus.Success:
                    importedCount++;
                    break;
            }
        }

        string summary = files.Count == 0
            ? "No files selected"
            : $"{files.Count:N0} file{(files.Count == 1 ? string.Empty : "s")} selected";

        return new DocumentImportQueueState(
            summary,
            CanImport: !isImporting && importableCount > 0,
            ImportableFileCount: importableCount,
            ImportedFileCount: importedCount,
            FailedFileCount: failedCount);
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

    private readonly record struct DocumentImportQueueState(
        string QueueSummary,
        bool CanImport,
        int ImportableFileCount,
        int ImportedFileCount,
        int FailedFileCount);
}
