namespace CogntApp.Models.Editor;

public sealed class CreateExtractRequestedEventArgs(
    string sourcePath,
    string selectedContent,
    string selectedPlainText) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string SelectedContent { get; } = selectedContent;

    public string SelectedPlainText { get; } = selectedPlainText;
}
