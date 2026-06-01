namespace CogntApp.Shell.Collections;

public interface IKnownCollectionStore
{
    IReadOnlyList<string> LoadCollectionPaths();

    string? LoadRecentCollectionPath();

    void RememberCollectionPath(string path);

    void ForgetCollectionPath(string path);

    void HideCollectionPath(string path);

    void UnhideCollectionPath(string path);

    bool IsCollectionPathHidden(string path);
}
