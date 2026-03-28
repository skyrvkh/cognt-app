namespace CogntApp.Models.Editor;

public sealed class OpenMentionRequestedEventArgs(
    string sourcePath,
    string relativePath,
    string markdown) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string RelativePath { get; } = relativePath;

    public string Markdown { get; } = markdown;
}
