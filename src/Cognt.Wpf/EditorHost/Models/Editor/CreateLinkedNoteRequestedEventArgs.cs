namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class CreateLinkedNoteRequestedEventArgs : EventArgs
{
    public CreateLinkedNoteRequestedEventArgs(
        string sourcePath,
        string viewMode)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        ViewMode = string.IsNullOrWhiteSpace(viewMode)
            ? throw new ArgumentException("View mode is required.", nameof(viewMode))
            : viewMode;
    }

    public string SourcePath { get; }

    public string ViewMode { get; }

    public CreateLinkedNoteResult Result { get; private set; } =
        CreateLinkedNoteResult.Fail("No linked note handler is registered.");

    public void Complete(CreateLinkedNoteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed record CreateLinkedNoteResult(
    bool Ok,
    string? RelativePath = null,
    string? Title = null,
    string? DocumentContent = null,
    string? Error = null)
{
    public static CreateLinkedNoteResult Success(
        string relativePath,
        string title,
        string documentContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(documentContent);

        return new CreateLinkedNoteResult(true, relativePath, title, documentContent);
    }

    public static CreateLinkedNoteResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new CreateLinkedNoteResult(false, Error: error);
    }
}
