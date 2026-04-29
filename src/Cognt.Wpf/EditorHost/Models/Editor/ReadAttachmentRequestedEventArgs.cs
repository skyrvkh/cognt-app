namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class ReadAttachmentRequestedEventArgs : EventArgs
{
    public ReadAttachmentRequestedEventArgs(
        string sourcePath,
        string imageSource)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        ImageSource = string.IsNullOrWhiteSpace(imageSource)
            ? throw new ArgumentException("Image source is required.", nameof(imageSource))
            : imageSource;
    }

    public string SourcePath { get; }

    public string ImageSource { get; }

    public ReadAttachmentResult Result { get; private set; } =
        ReadAttachmentResult.Fail("No attachment reader is registered.");

    public void Complete(ReadAttachmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }
}

public sealed record ReadAttachmentResult(
    bool Ok,
    string? FileName = null,
    string? RelativePath = null,
    string? ContentType = null,
    string? ContentBase64 = null,
    string? Error = null)
{
    public static ReadAttachmentResult Success(
        string fileName,
        string relativePath,
        string contentType,
        string contentBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentBase64);

        return new ReadAttachmentResult(true, fileName, relativePath, contentType, contentBase64);
    }

    public static ReadAttachmentResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ReadAttachmentResult(false, Error: error);
    }
}
