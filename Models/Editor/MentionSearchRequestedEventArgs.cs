using CogntApp.Models.Markdown;

namespace CogntApp.Models.Editor;

public sealed class MentionSearchRequestedEventArgs(
    string sourcePath,
    string query,
    int limit) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string Query { get; } = query;

    public int Limit { get; } = limit;

    public IReadOnlyList<MentionTarget> Targets { get; private set; } = [];

    public void Complete(IEnumerable<MentionTarget> targets)
    {
        Targets = targets.ToArray();
    }
}
