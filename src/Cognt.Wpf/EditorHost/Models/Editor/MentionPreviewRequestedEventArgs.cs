namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class MentionPreviewRequestedEventArgs : EventArgs
{
    public MentionPreviewRequestedEventArgs(
        string requestId,
        string sourcePath,
        string relativePath,
        string mode,
        int maxCharacters)
    {
        RequestId = string.IsNullOrWhiteSpace(requestId)
            ? throw new ArgumentException("Request id is required.", nameof(requestId))
            : requestId;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        RelativePath = string.IsNullOrWhiteSpace(relativePath)
            ? throw new ArgumentException("Relative path is required.", nameof(relativePath))
            : relativePath;
        Mode = string.IsNullOrWhiteSpace(mode)
            ? throw new ArgumentException("Preview mode is required.", nameof(mode))
            : mode;
        MaxCharacters = Math.Max(1, maxCharacters);
    }

    public string RequestId { get; }

    public string SourcePath { get; }

    public string RelativePath { get; }

    public string Mode { get; }

    public int MaxCharacters { get; }
}
