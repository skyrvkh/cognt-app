namespace CogntApp.Models.Editor;

public sealed class CreateExtractRequestedEventArgs(
    string sourcePath,
    string selectedMarkdown,
    string selectedPlainText) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string SelectedMarkdown { get; } = selectedMarkdown;

    public string SelectedPlainText { get; } = selectedPlainText;
}
