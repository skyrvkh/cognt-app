using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CogntApp.Shell.Dialogs;

internal sealed partial class WindowShellDialogService
{
    public string? PromptForExistingCollectionPath(
        string title,
        string defaultParentPath,
        string collectionDirectoryExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultParentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionDirectoryExtension);

        OpenFolderDialog folderDialog = new()
        {
            Title = title,
            InitialDirectory = Directory.Exists(defaultParentPath)
                ? defaultParentPath
                : Environment.CurrentDirectory,
            Multiselect = false,
        };

        if (folderDialog.ShowDialog(ownerWindow) != true)
        {
            return null;
        }

        string selectedPath = folderDialog.FolderName.Trim();
        string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedPath));
        if (!directoryName.EndsWith(collectionDirectoryExtension, StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning(
                title,
                $"Choose a collection folder ending with \"{collectionDirectoryExtension}\".");
            return null;
        }

        return selectedPath;
    }

    private void ShowCollectionLocationPicker(TextBox locationTextBox, string defaultParentPath)
    {
        OpenFolderDialog folderDialog = new()
        {
            Title = "Choose collection location",
            InitialDirectory = Directory.Exists(locationTextBox.Text)
                ? locationTextBox.Text
                : defaultParentPath,
            Multiselect = false,
        };

        if (folderDialog.ShowDialog(ownerWindow) == true)
        {
            locationTextBox.Text = folderDialog.FolderName;
        }
    }
}
