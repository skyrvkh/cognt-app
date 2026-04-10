namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class OpenMentionRequestedEventArgs : EventArgs
{
    public OpenMentionRequestedEventArgs(
        string sourcePath,
        string relativePath,
        string content)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        RelativePath = string.IsNullOrWhiteSpace(relativePath)
            ? throw new ArgumentException("Relative path is required.", nameof(relativePath))
            : relativePath;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string SourcePath { get; }

    public string RelativePath { get; }

    public string Content { get; }
}
