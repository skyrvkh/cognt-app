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
        DocumentImportQueueItem[] importableFiles = Files
            .Where(file => file.Status is DocumentImportStatus.Ready or DocumentImportStatus.Failed)
            .ToArray();

        bool openAfterImport = OpenImportedDocumentAfterImport && importableFiles.Length == 1;

        foreach (DocumentImportQueueItem file in importableFiles)
        {
            await ImportQueuedFileAsync(file, openAfterImport);
        }

        IsImporting = false;

        int imported = Files.Count(file => file.Status == DocumentImportStatus.Success);
        int failed = Files.Count(file => file.Status == DocumentImportStatus.Failed);
        UpdateMessageState(
            failed == 0
                ? $"Imported {imported:N0} document{(imported == 1 ? string.Empty : "s")}."
                : $"Imported {imported:N0} document{(imported == 1 ? string.Empty : "s")}. {failed:N0} failed.",
            isError: failed > 0);
        RefreshDerivedState();
    }

    private Task ImportQueuedFileAsync(DocumentImportQueueItem file, bool openAfterImport)
    {
        try
        {
            file.ErrorMessage = string.Empty;
            file.Status = DocumentImportStatus.Importing;

            IReadOnlyList<string> importedPaths = target.ImportDocuments([file.FullPath]);
            if (openAfterImport && importedPaths.Count > 0 && target.OpenNoteCommand.CanExecute(importedPaths[0]))
            {
                target.OpenNoteCommand.Execute(importedPaths[0]);
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

        return Task.CompletedTask;
    }
}
