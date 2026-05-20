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

        TextBox nameTextBox = CreateCollectionPromptTextBox(defaultName);
        TextBox locationTextBox = CreateCollectionPromptTextBox(defaultParentPath);
        Button browseButton = CreateCollectionSecondaryButton("Browse");
        browseButton.Width = 104;
        browseButton.Margin = new Thickness(10, 0, 0, 0);

        Button cancelButton = CreateCollectionSecondaryButton("Cancel", isCancel: true);
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        Button createButton = CreateCollectionPrimaryButton(actionLabel, isDefault: true);

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

        Border iconBadge = new()
        {
            Width = 52,
            Height = 52,
            Background = ThemeResourceLookup.Brush("AccentSoftBrush", Brushes.LightBlue),
            BorderBrush = ThemeResourceLookup.Brush("AccentBrush", Brushes.SteelBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = CreateFolderPath(22, ThemeResourceLookup.Brush("AccentStrongBrush", Brushes.SteelBlue)),
        };

        StackPanel headerText = new()
        {
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerText.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = ThemeResourceLookup.Brush("InkBrush", Brushes.Black),
            FontFamily = ThemeResourceLookup.FontFamily("AppDisplayFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
        });
        headerText.Children.Add(new TextBlock
        {
            Text = "Choose a name and parent folder for the new workspace.",
            Foreground = ThemeResourceLookup.Brush("MutedBrush", Brushes.DimGray),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        Grid header = new()
        {
            Margin = new Thickness(0, 0, 0, 18),
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(iconBadge, 0);
        Grid.SetColumn(headerText, 1);
        header.Children.Add(iconBadge);
        header.Children.Add(headerText);

        StackPanel fieldsPanel = new();
        fieldsPanel.Children.Add(CreateCollectionPromptLabel("Collection name"));
        fieldsPanel.Children.Add(nameTextBox);
        fieldsPanel.Children.Add(CreateCollectionPromptLabel("Parent folder", new Thickness(0, 18, 0, 7)));
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

        Border fieldsCard = new()
        {
            Background = ThemeResourceLookup.Brush("PanelAltBrush", Brushes.WhiteSmoke),
            BorderBrush = ThemeResourceLookup.Brush("LineBrush", Brushes.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = fieldsPanel,
        };

        StackPanel actionsPanel = CreateDialogActionsPanel(new Thickness(0), cancelButton, createButton);
        Border actionsBar = new()
        {
            Background = ThemeResourceLookup.Brush("PanelBrush", Brushes.White),
            BorderBrush = ThemeResourceLookup.Brush("LineBrush", Brushes.LightGray),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 16, 24, 16),
            Child = actionsPanel,
        };

        StackPanel body = new()
        {
            Margin = new Thickness(24, 22, 24, 20),
        };
        body.Children.Add(header);
        body.Children.Add(fieldsCard);

        Grid contentGrid = new();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 0);
        Grid.SetRow(actionsBar, 1);
        contentGrid.Children.Add(body);
        contentGrid.Children.Add(actionsBar);

        Border dialogContent = new()
        {
            Background = ThemeResourceLookup.Brush("PanelBrush", Brushes.White),
            Child = contentGrid,
        };

        Window dialog = CreateDialogWindow(title, height: 470, width: 620, dialogContent);
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

    private static TextBlock CreateCollectionPromptLabel(string text, Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = ThemeResourceLookup.Brush("MutedBrush", Brushes.DimGray),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = margin ?? new Thickness(0, 0, 0, 7),
        };
    }

    private static TextBox CreateCollectionPromptTextBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Height = 42,
            Padding = new Thickness(12, 7, 12, 7),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = ThemeResourceLookup.Brush("ControlBrush", Brushes.White),
            BorderBrush = ThemeResourceLookup.Brush("ControlBorderBrush", Brushes.LightGray),
            Foreground = ThemeResourceLookup.Brush("InkBrush", Brushes.Black),
            BorderThickness = new Thickness(1),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 14,
        };
    }

    private static Button CreateCollectionPrimaryButton(string label, bool isDefault = false)
    {
        Button button = CreateCollectionButton(label, isDefault: isDefault);
        button.Background = ThemeResourceLookup.Brush("AccentBrush", Brushes.SteelBlue);
        button.BorderBrush = ThemeResourceLookup.Brush("AccentBrush", Brushes.SteelBlue);
        button.Foreground = ThemeResourceLookup.Brush("OnAccentBrush", Brushes.White);
        return button;
    }

    private static Button CreateCollectionSecondaryButton(string label, bool isCancel = false)
    {
        Button button = CreateCollectionButton(label, isCancel: isCancel);
        button.Background = ThemeResourceLookup.Brush("ControlBrush", Brushes.White);
        button.BorderBrush = ThemeResourceLookup.Brush("ControlBorderBrush", Brushes.LightGray);
        button.Foreground = ThemeResourceLookup.Brush("InkBrush", Brushes.Black);
        return button;
    }

    private static Button CreateCollectionButton(string label, bool isDefault = false, bool isCancel = false)
    {
        return new Button
        {
            Content = label,
            Height = 42,
            MinWidth = 104,
            Padding = new Thickness(18, 0, 18, 0),
            BorderThickness = new Thickness(1),
            IsDefault = isDefault,
            IsCancel = isCancel,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
    }

    private static FrameworkElement CreateFolderPath(double size, Brush stroke)
    {
        System.Windows.Shapes.Path path = new()
        {
            Data = Geometry.Parse("M2.5,6.8 L8,6.8 L9.8,8.7 L21.5,8.7 L21.5,18.5 L2.5,18.5 Z"),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Stroke = stroke,
            StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return path;
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
