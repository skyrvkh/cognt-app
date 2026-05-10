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

    public string QueueSummary => Files.Count == 0
        ? "No files selected"
        : $"{Files.Count:N0} file{(Files.Count == 1 ? string.Empty : "s")} selected";

    public bool CanImport => !IsImporting
        && Files.Any(file => file.Status is DocumentImportStatus.Ready or DocumentImportStatus.Failed);

    public string ImportButtonText => Files.Count == 0 ? "Add files to import" : "Import";

    internal void AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        List<string> rejected = [];
        HashSet<string> existingPaths = Files
            .Select(file => Path.GetFullPath(file.FullPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    private void UpdateMessageState(string value, bool isError)
    {
        IsMessageError = isError;
        Message = value;
    }
}
