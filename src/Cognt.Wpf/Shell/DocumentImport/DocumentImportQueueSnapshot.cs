namespace CogntApp.Shell.DocumentImport;

internal readonly record struct DocumentImportQueueSnapshot(
    string QueueSummary,
    bool CanImport,
    int ImportableFileCount,
    int ImportedFileCount,
    int FailedFileCount)
{
    internal static DocumentImportQueueSnapshot Empty { get; } = new(
        "No files selected",
        CanImport: false,
        ImportableFileCount: 0,
        ImportedFileCount: 0,
        FailedFileCount: 0);

    internal bool HasFailures => FailedFileCount > 0;

    internal static DocumentImportQueueSnapshot Create(
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

        return new DocumentImportQueueSnapshot(
            summary,
            CanImport: !isImporting && importableCount > 0,
            ImportableFileCount: importableCount,
            ImportedFileCount: importedCount,
            FailedFileCount: failedCount);
    }

    internal string FormatCompletionMessage()
    {
        string imported = $"Imported {ImportedFileCount:N0} document{(ImportedFileCount == 1 ? string.Empty : "s")}.";
        return FailedFileCount == 0
            ? imported
            : $"{imported} {FailedFileCount:N0} failed.";
    }
}
