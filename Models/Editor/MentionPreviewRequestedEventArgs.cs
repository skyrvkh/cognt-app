namespace CogntApp.Models.Editor;

public sealed class MentionPreviewRequestedEventArgs(
    string requestId,
    string sourcePath,
    string relativePath,
    string mode,
    int maxCharacters) : EventArgs
{
    public string RequestId { get; } = requestId;

    public string SourcePath { get; } = sourcePath;

    public string RelativePath { get; } = relativePath;

    public string Mode { get; } = mode;

    public int MaxCharacters { get; } = maxCharacters;
}
