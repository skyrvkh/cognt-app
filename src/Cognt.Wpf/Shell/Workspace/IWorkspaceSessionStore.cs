using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.Shell.Workspace;

public interface IWorkspaceSessionStore
{
    WorkspaceSessionSnapshot Load(string collectionPath);

    void Save(WorkspaceSessionSnapshot snapshot);

    void Forget(string collectionPath);
}

public sealed class WorkspaceSessionSnapshot
{
    public string CollectionPath { get; init; } = string.Empty;

    public string? ActiveNotePath { get; init; }

    public IReadOnlyList<WorkspaceSessionNote> OpenNotes { get; init; } = [];
}

public sealed class WorkspaceSessionNote
{
    public string NotePath { get; init; } = string.Empty;

    public EditorCaretState? Caret { get; init; }
}
