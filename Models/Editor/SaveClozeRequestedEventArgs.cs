namespace CogntApp.Models.Editor;

public sealed record SaveClozeRequestedEventArgs(
    string SourcePath,
    string SelectedPlainText,
    string ClozeText,
    string Content);
