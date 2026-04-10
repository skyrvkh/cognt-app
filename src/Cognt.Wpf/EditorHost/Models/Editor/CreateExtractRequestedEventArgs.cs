namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class CreateExtractRequestedEventArgs : EventArgs
{
    public CreateExtractRequestedEventArgs(
        string sourcePath,
        string selectedContent,
        string selectedPlainText)
    {
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
            : sourcePath;
        SelectedContent = selectedContent ?? throw new ArgumentNullException(nameof(selectedContent));
        SelectedPlainText = string.IsNullOrWhiteSpace(selectedPlainText)
            ? throw new ArgumentException("Selected text is required.", nameof(selectedPlainText))
            : selectedPlainText;
    }

    public string SourcePath { get; }

    public string SelectedContent { get; }

    public string SelectedPlainText { get; }
}
