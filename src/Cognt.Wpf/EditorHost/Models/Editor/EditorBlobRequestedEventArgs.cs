namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class StoreEditorBlobRequestedEventArgs : EventArgs
{
    public StoreEditorBlobRequestedEventArgs(
        string sourcePath,
        string blobId,
        string contentType,
        string contentBase64)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        BlobId = string.IsNullOrWhiteSpace(blobId)
            ? throw new ArgumentException("Blob id is required.", nameof(blobId))
            : blobId;
        ContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;
        ContentBase64 = string.IsNullOrWhiteSpace(contentBase64)
            ? throw new ArgumentException("Blob content is required.", nameof(contentBase64))
            : contentBase64;
    }

    public string SourcePath { get; }

    public string BlobId { get; }

    public string ContentType { get; }

    public string ContentBase64 { get; }

    public EditorBlobResult Result { get; private set; } =
        EditorBlobResult.Fail("No editor blob writer is registered.");

    public void Complete(EditorBlobResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed class ReadEditorBlobRequestedEventArgs : EventArgs
{
    public ReadEditorBlobRequestedEventArgs(
        string sourcePath,
        string blobId)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        BlobId = string.IsNullOrWhiteSpace(blobId)
            ? throw new ArgumentException("Blob id is required.", nameof(blobId))
            : blobId;
    }

    public string SourcePath { get; }

    public string BlobId { get; }

    public EditorBlobReadResult Result { get; private set; } =
        EditorBlobReadResult.Fail("No editor blob reader is registered.");

    public void Complete(EditorBlobReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed class ListEditorBlobsRequestedEventArgs : EventArgs
{
    public ListEditorBlobsRequestedEventArgs(string sourcePath)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
    }

    public string SourcePath { get; }

    public EditorBlobListResult Result { get; private set; } =
        EditorBlobListResult.Fail("No editor blob lister is registered.");

    public void Complete(EditorBlobListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed record EditorBlobResult(
    bool Ok,
    string? Error = null)
{
    public static EditorBlobResult Success()
    {
        return new EditorBlobResult(true);
    }

    public static EditorBlobResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new EditorBlobResult(false, error);
    }
}

public sealed record EditorBlobReadResult(
    bool Ok,
    string? ContentType = null,
    string? ContentBase64 = null,
    string? Error = null)
{
    public static EditorBlobReadResult Success(string contentType, string contentBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentBase64);

        return new EditorBlobReadResult(true, contentType, contentBase64);
    }

    public static EditorBlobReadResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new EditorBlobReadResult(false, Error: error);
    }
}

public sealed record EditorBlobListResult(
    bool Ok,
    IReadOnlyList<string>? BlobIds = null,
    string? Error = null)
{
    public static EditorBlobListResult Success(IReadOnlyList<string> blobIds)
    {
        ArgumentNullException.ThrowIfNull(blobIds);

        return new EditorBlobListResult(true, blobIds);
    }

    public static EditorBlobListResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new EditorBlobListResult(false, Error: error);
    }
}
