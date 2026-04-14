namespace CogntApp.Shell.Collections;

public interface IKnowledgeCollectionPathResolver
{
    string CollectionDirectoryExtension { get; }

    string ResolveDefaultCollectionPath();

    bool IsCollectionPath(string path);
}
