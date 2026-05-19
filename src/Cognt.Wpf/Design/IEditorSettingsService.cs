namespace CogntApp.Design;

public interface IEditorSettingsService
{
    EditorSettings Load();

    void Save(EditorSettings settings);
}
