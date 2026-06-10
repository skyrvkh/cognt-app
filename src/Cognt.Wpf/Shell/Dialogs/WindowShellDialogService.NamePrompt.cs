using System.Windows;
using System.Windows.Controls;
using CogntApp.Design;

namespace CogntApp.Shell.Dialogs;

internal sealed partial class WindowShellDialogService
{
    public string? PromptForName(string title, string prompt, string currentName, string actionLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);

        TextBlock promptTextBlock = CreateDialogLabel(
            prompt,
            ThemeResourceLookup.Brush("InkBrush", System.Windows.Media.Brushes.Black),
            13,
            new Thickness(0, 0, 0, 8));
        TextBox nameTextBox = CreateDialogTextBox(currentName, 30, new Thickness(8, 3, 8, 3), 13);
        Button cancelButton = CreateDialogActionButton("Cancel", isCancel: true, margin: new Thickness(0, 0, 8, 0));
        Button confirmButton = CreateDialogActionButton(actionLabel, isDefault: true);
        StackPanel actionsPanel = CreateDialogActionsPanel(new Thickness(0, 14, 0, 0), cancelButton, confirmButton);

        Grid dialogContent = new()
        {
            Margin = new Thickness(16),
        };
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(promptTextBlock, 0);
        Grid.SetRow(nameTextBox, 1);
        Grid.SetRow(actionsPanel, 2);
        dialogContent.Children.Add(promptTextBlock);
        dialogContent.Children.Add(nameTextBox);
        dialogContent.Children.Add(actionsPanel);

        Window dialog = CreateDialogWindow(title, height: 150, width: 360, dialogContent);
        FocusAndSelectTextBox(dialog, nameTextBox);
        confirmButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                ShowWarning(title, "Name is required.");
                return;
            }

            dialog.DialogResult = true;
        };

        if (dialog.ShowDialog() == true)
        {
            return nameTextBox.Text.Trim();
        }

        return null;
    }
}
