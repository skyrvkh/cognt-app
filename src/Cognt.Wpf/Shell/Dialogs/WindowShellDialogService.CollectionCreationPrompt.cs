using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CogntApp.Shell.Dialogs;

internal sealed partial class WindowShellDialogService
{
    public CollectionCreationPromptResult? PromptForCollectionCreation(
        string title,
        string defaultName,
        string defaultParentPath,
        string actionLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(defaultName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultParentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);

        TextBox nameTextBox = CreatePromptTextBox(defaultName);
        TextBox locationTextBox = CreatePromptTextBox(defaultParentPath);
        Button browseButton = new()
        {
            Content = "Browse",
            Width = 78,
            Height = 30,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Button cancelButton = CreateDialogActionButton("Cancel", isCancel: true, margin: new Thickness(0, 0, 8, 0));
        Button createButton = CreateDialogActionButton(actionLabel, isDefault: true);

        Grid locationGrid = new();
        locationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(locationTextBox, 0);
        Grid.SetColumn(browseButton, 1);
        locationGrid.Children.Add(locationTextBox);
        locationGrid.Children.Add(browseButton);

        StackPanel fieldsPanel = new()
        {
            Margin = new Thickness(16),
        };
        fieldsPanel.Children.Add(CreatePromptLabel("Collection name"));
        fieldsPanel.Children.Add(nameTextBox);
        fieldsPanel.Children.Add(CreatePromptLabel("Location", new Thickness(0, 14, 0, 8)));
        fieldsPanel.Children.Add(locationGrid);

        StackPanel actionsPanel = CreateDialogActionsPanel(new Thickness(16, 2, 16, 16), cancelButton, createButton);
        DockPanel dialogContent = new();
        DockPanel.SetDock(actionsPanel, Dock.Bottom);
        dialogContent.Children.Add(actionsPanel);
        dialogContent.Children.Add(fieldsPanel);

        Window dialog = CreateDialogWindow(title, height: 230, width: 480, dialogContent);
        FocusAndSelectTextBox(dialog, nameTextBox);
        browseButton.Click += (_, _) => ShowCollectionLocationPicker(locationTextBox, defaultParentPath);
        createButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                ShowWarning(title, "Collection name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(locationTextBox.Text) || !Directory.Exists(locationTextBox.Text))
            {
                ShowWarning(title, "Choose an existing folder for the collection location.");
                return;
            }

            dialog.DialogResult = true;
        };

        return dialog.ShowDialog() == true
            ? new CollectionCreationPromptResult(nameTextBox.Text.Trim(), locationTextBox.Text.Trim())
            : null;
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
