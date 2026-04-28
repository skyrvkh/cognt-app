namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class AttachFileRequestedEventArgs : EventArgs
{
    public AttachFileRequestedEventArgs(
        string sourcePath,
        string fileName,
        string contentBase64)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("File name is required.", nameof(fileName))
            : fileName;
        ContentBase64 = string.IsNullOrWhiteSpace(contentBase64)
            ? throw new ArgumentException("File content is required.", nameof(contentBase64))
            : contentBase64;
    }

    public string SourcePath { get; }

    public string FileName { get; }

    public string ContentBase64 { get; }

    public AttachFileResult Result { get; private set; } =
        AttachFileResult.Fail("No attachment handler is registered.");

    public void Complete(AttachFileResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed record AttachFileResult(
    bool Ok,
    string? DisplayName = null,
    string? RelativePath = null,
    string? Kind = null,
    string? Error = null)
{
    public static AttachFileResult Success(string displayName, string relativePath, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        return new AttachFileResult(true, displayName, relativePath, kind);
    }

    public static AttachFileResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new AttachFileResult(false, Error: error);
    }
}
