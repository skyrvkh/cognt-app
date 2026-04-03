namespace CogntApp.Models.Editor;

public sealed class OpenMentionRequestedEventArgs(
    string sourcePath,
    string relativePath,
    string content) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string RelativePath { get; } = relativePath;

    public string Content { get; } = content;
}
