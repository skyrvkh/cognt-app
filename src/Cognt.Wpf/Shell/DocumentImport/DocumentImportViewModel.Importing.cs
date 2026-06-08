using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel
{
    internal async Task ImportExistingCollectionAsync(string collectionPath)
    {
        if (IsImporting)
        {
            return;
        }

        IsImporting = true;
        UpdateMessageState(string.Empty, isError: false);

        try
        {
            string message = await target.ImportExistingCollectionAsync(collectionPath);
            UpdateMessageState(message, isError: false);
        }
        catch (Exception ex)
        {
            UpdateMessageState(ex.Message, isError: true);
            logger?.LogError(ex, "Collection import failed. CollectionPath={CollectionPath}", collectionPath);
        }
        finally
        {
            IsImporting = false;
            RefreshDerivedState();
        }
    }

    private async Task ImportAsync()
    {
        if (!CanImport)
        {
            return;
        }

        IsImporting = true;
        UpdateMessageState(string.Empty, isError: false);
        DocumentImportQueueItem[] importableFiles = CreateImportableFileSnapshot();

        bool openAfterImport = OpenImportedDocumentAfterImport && importableFiles.Length == 1;

        foreach (DocumentImportQueueItem file in importableFiles)
        {
            await ImportQueuedFileAsync(file, openAfterImport);
        }

        IsImporting = false;

        UpdateMessageState(
            queueState.FormatCompletionMessage(),
            isError: queueState.HasFailures);
        RefreshDerivedState();
        logger?.LogInformation(
            "Document import batch completed. TotalFiles={TotalFiles} SuccessCount={SuccessCount} FailedCount={FailedCount}",
            Files.Count,
            Files.Count(file => file.Status == DocumentImportStatus.Success),
            Files.Count(file => file.Status == DocumentImportStatus.Failed));
    }

    private DocumentImportQueueItem[] CreateImportableFileSnapshot()
    {
        List<DocumentImportQueueItem> importableFiles = new(
            queueState.ImportableFileCount > 0 ? queueState.ImportableFileCount : Files.Count);
        foreach (DocumentImportQueueItem file in Files)
        {
            if (file.Status is DocumentImportStatus.Ready or DocumentImportStatus.Failed)
            {
                importableFiles.Add(file);
            }
        }

        return importableFiles.ToArray();
    }

    private async Task ImportQueuedFileAsync(DocumentImportQueueItem file, bool openAfterImport)
    {
        try
        {
            file.ErrorMessage = string.Empty;
            file.Status = DocumentImportStatus.Importing;

            switch (file.Type)
            {
                case DocumentImportType.Docx:
                    await target.ImportNativeDocumentAsync(
                        file.FullPath,
                        NativeDocumentImportFormat.Docx,
                        openAfterImport);
                    break;
                case DocumentImportType.Markdown:
                    await target.ImportNativeDocumentAsync(
                        file.FullPath,
                        NativeDocumentImportFormat.Markdown,
                        openAfterImport);
                    break;
                case DocumentImportType.Pdf:
                    await target.ImportPdfDocumentAsync(file.FullPath, openAfterImport);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported document import type.");
            }

            file.Status = DocumentImportStatus.Success;
        }
        catch (Exception ex)
        {
            file.ErrorMessage = ex.Message;
            file.Status = DocumentImportStatus.Failed;
            logger?.LogError(
                ex,
                "Document import file failed. Path={Path} Type={Type} Mode={Mode}",
                file.FullPath,
                file.Type,
                file.Mode);
        }
        finally
        {
            RefreshDerivedState();
        }
    }
}
