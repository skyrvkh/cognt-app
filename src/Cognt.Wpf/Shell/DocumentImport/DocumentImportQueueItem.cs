using System.IO;
using CogntApp.ViewModels;

namespace CogntApp.Shell.DocumentImport;

internal sealed class DocumentImportQueueItem : ObservableObject
{
    private DocumentImportStatus status = DocumentImportStatus.Ready;
    private string errorMessage = string.Empty;

    internal DocumentImportQueueItem(string fullPath, long fileSizeBytes, DocumentImportType type)
    {
        FullPath = fullPath;
        FileName = System.IO.Path.GetFileName(fullPath);
        Type = type;
        Mode = DocumentImportPolicy.GetImportMode(type);
        TypeLabel = DocumentImportPolicy.GetTypeLabel(type);
        FileSizeLabel = DocumentImportPolicy.FormatFileSize(fileSizeBytes);
        ImportModeLabel = DocumentImportPolicy.GetModeLabel(Mode);
    }

    public string FullPath { get; }

    public string FileName { get; }

    public DocumentImportType Type { get; }

    public DocumentImportMode Mode { get; }

    public string TypeLabel { get; }

    public string FileSizeLabel { get; }

    public string ImportModeLabel { get; }

    public DocumentImportStatus Status
    {
        get => status;
        set
        {
            if (SetProperty(ref status, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusTone));
                OnPropertyChanged(nameof(CanRemove));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string StatusLabel => Status switch
    {
        DocumentImportStatus.Importing => "Importing",
        DocumentImportStatus.Success => "Imported",
        DocumentImportStatus.Failed => "Failed",
        _ => "Ready"
    };

    public string StatusTone => Status switch
    {
        DocumentImportStatus.Success => "Success",
        DocumentImportStatus.Failed => "Failed",
        DocumentImportStatus.Importing => "Importing",
        _ => "Ready"
    };

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (SetProperty(ref errorMessage, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => Status == DocumentImportStatus.Failed && !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanRemove => Status is DocumentImportStatus.Ready or DocumentImportStatus.Failed;
}
