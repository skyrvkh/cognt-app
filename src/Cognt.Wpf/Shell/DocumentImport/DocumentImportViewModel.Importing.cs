using CogntApp.ViewModels;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel
{
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
            failedFileCount == 0
                ? $"Imported {importedFileCount:N0} document{(importedFileCount == 1 ? string.Empty : "s")}."
                : $"Imported {importedFileCount:N0} document{(importedFileCount == 1 ? string.Empty : "s")}. {failedFileCount:N0} failed.",
            isError: failedFileCount > 0);
        RefreshDerivedState();
    }

    private DocumentImportQueueItem[] CreateImportableFileSnapshot()
    {
        List<DocumentImportQueueItem> importableFiles = new(importableFileCount > 0 ? importableFileCount : Files.Count);
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
        }
        finally
        {
            RefreshDerivedState();
        }
    }
}
