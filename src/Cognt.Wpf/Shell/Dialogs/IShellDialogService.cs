namespace CogntApp.Shell.Dialogs;

internal interface IShellDialogService
{
    void ShowInfo(string title, string message);

    void ShowWarning(string title, string message);

    bool ConfirmWarning(string title, string message);

    string? PromptForName(string title, string prompt, string currentName, string actionLabel);

    CollectionCreationPromptResult? PromptForCollectionCreation(
        string title,
        string defaultName,
        string defaultParentPath,
        string actionLabel);

    string? PromptForExistingCollectionPath(
        string title,
        string defaultParentPath,
        string collectionDirectoryExtension);
}

internal sealed record CollectionCreationPromptResult(
    string Name,
    string ParentPath);
