using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class MentionSearchRequestedEventArgs : EventArgs
{
    public MentionSearchRequestedEventArgs(
        string sourcePath,
        string query,
        int limit)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        Query = query ?? throw new ArgumentNullException(nameof(query));
        Limit = Math.Max(1, limit);
    }

    public string SourcePath { get; }

    public string Query { get; }

    public int Limit { get; }

    public IReadOnlyList<KnowledgeMentionTargetReadModel> Targets { get; private set; } = [];

    public void Complete(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        Targets = targets.ToArray();
    }
}
