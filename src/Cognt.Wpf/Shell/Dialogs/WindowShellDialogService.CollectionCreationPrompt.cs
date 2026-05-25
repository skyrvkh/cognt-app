using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CogntApp.Design;
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

        Brush mutedBrush = ThemeResourceLookup.Brush("MutedBrush", Brushes.DimGray);
        Brush controlBrush = ThemeResourceLookup.Brush("ControlBrush", Brushes.White);
        Brush controlBorderBrush = ThemeResourceLookup.Brush("ControlBorderBrush", Brushes.LightGray);
        Brush inkBrush = ThemeResourceLookup.Brush("InkBrush", Brushes.Black);
        Brush accentBrush = ThemeResourceLookup.Brush("AccentBrush", Brushes.SteelBlue);
        Brush vibrantButtonForegroundBrush = ThemeResourceLookup.Brush("VibrantButtonForegroundBrush", Brushes.White);

        TextBox nameTextBox = CreateDialogTextBox(defaultName, 42, new Thickness(12, 7, 12, 7), 14, controlBrush, controlBorderBrush, inkBrush);
        TextBox locationTextBox = CreateDialogTextBox(defaultParentPath, 42, new Thickness(12, 7, 12, 7), 14, controlBrush, controlBorderBrush, inkBrush);
        Button browseButton = CreateThemedDialogButton(
            "Browse",
            controlBrush,
            controlBorderBrush,
            inkBrush,
            42,
            104,
            new Thickness(18, 0, 18, 0),
            14,
            FontWeights.SemiBold,
            margin: new Thickness(10, 0, 0, 0));

        Button cancelButton = CreateThemedDialogButton(
            "Cancel",
            controlBrush,
            controlBorderBrush,
            inkBrush,
            42,
            104,
            new Thickness(18, 0, 18, 0),
            14,
            FontWeights.SemiBold,
            isCancel: true,
            margin: new Thickness(0, 0, 10, 0));
        Button createButton = CreateThemedDialogButton(
            actionLabel,
            accentBrush,
            accentBrush,
            vibrantButtonForegroundBrush,
            42,
            104,
            new Thickness(18, 0, 18, 0),
            14,
            FontWeights.SemiBold,
            isDefault: true,
            styleKey: "PrimaryButtonStyle");

        Grid locationGrid = new()
        {
            Margin = new Thickness(0, 0, 0, 0),
        };
        locationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(locationTextBox, 0);
        Grid.SetColumn(browseButton, 1);
        locationGrid.Children.Add(locationTextBox);
        locationGrid.Children.Add(browseButton);

        StackPanel header = new()
        {
            Margin = new Thickness(0, 0, 0, 22),
        };
        header.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ThemeResourceLookup.Brush("InkBrush", Brushes.Black),
            FontFamily = ThemeResourceLookup.FontFamily("AppDisplayFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = "Choose a name and parent folder for the new workspace.",
            Foreground = ThemeResourceLookup.Brush("MutedBrush", Brushes.DimGray),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
            Margin = new Thickness(0, 7, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        StackPanel fieldsPanel = new();
        fieldsPanel.Children.Add(CreateDialogLabel("Collection name", mutedBrush, 12, fontWeight: FontWeights.SemiBold, margin: new Thickness(0, 0, 0, 7)));
        fieldsPanel.Children.Add(nameTextBox);
        fieldsPanel.Children.Add(CreateDialogLabel("Parent folder", mutedBrush, 12, new Thickness(0, 18, 0, 7), FontWeights.SemiBold));
        fieldsPanel.Children.Add(locationGrid);
        fieldsPanel.Children.Add(new TextBlock
        {
            Text = "The app will create a .cognt folder inside this location.",
            Foreground = ThemeResourceLookup.Brush("DimBrush", Brushes.Gray),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        StackPanel actionsPanel = CreateDialogActionsPanel(new Thickness(0, 24, 0, 0), cancelButton, createButton);

        StackPanel body = new()
        {
            Margin = new Thickness(0),
        };
        body.Children.Add(header);
        body.Children.Add(fieldsPanel);

        Grid contentGrid = new()
        {
            Margin = new Thickness(24),
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 0);
        Grid.SetRow(actionsPanel, 1);
        contentGrid.Children.Add(body);
        contentGrid.Children.Add(actionsPanel);

        Window dialog = CreateDialogWindow(title, height: 398, width: 560, contentGrid);
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
