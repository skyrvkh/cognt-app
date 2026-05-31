namespace CogntApp.Design;

public interface IEditorSettingsService
{
    EditorSettings Load(string? collectionPath = null);

    void Save(EditorSettings settings, string? collectionPath = null);

    void EnsureCollectionSettingsFile(string collectionPath);
}
