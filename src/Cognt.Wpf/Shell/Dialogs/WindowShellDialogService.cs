using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CogntApp.Design;

namespace CogntApp.Shell.Dialogs;

internal sealed partial class WindowShellDialogService(Window owner) : IShellDialogService
{
    private readonly Window ownerWindow = owner ?? throw new ArgumentNullException(nameof(owner));

    public void ShowInfo(string title, string message)
    {
        ShowMessageBox(title, message, MessageBoxImage.Information);
    }

    public void ShowWarning(string title, string message)
    {
        ShowMessageBox(title, message, MessageBoxImage.Warning);
    }

    public bool ConfirmWarning(string title, string message)
    {
        EnsureMessageDialogArgs(title, message);

        return MessageBox.Show(ownerWindow, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
    }

    private Window CreateDialogWindow(string title, double height, double width, object content)
    {
        return new Window
        {
            Title = title,
            Height = height,
            Width = width,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ThemeResourceLookup.Brush("PanelBrush", Brushes.White),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
            Owner = ownerWindow,
            Content = content,
        };
    }

    private static Button CreateDialogActionButton(string label, bool isDefault = false, bool isCancel = false, Thickness? margin = null)
    {
        return new Button
        {
            Content = label,
            Width = 76,
            Height = 28,
            Margin = margin ?? default,
            IsDefault = isDefault,
            IsCancel = isCancel,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
        };
    }

    private static StackPanel CreateDialogActionsPanel(Thickness margin, params Button[] buttons)
    {
        StackPanel actionsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = margin,
        };

        foreach (Button button in buttons)
        {
            actionsPanel.Children.Add(button);
        }

        return actionsPanel;
    }

    private static void FocusAndSelectTextBox(Window dialog, TextBox textBox)
    {
        dialog.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
    }

    private void ShowMessageBox(string title, string message, MessageBoxImage image)
    {
        EnsureMessageDialogArgs(title, message);

        MessageBox.Show(ownerWindow, message, title, MessageBoxButton.OK, image);
    }

    private static void EnsureMessageDialogArgs(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
    }

    private static TextBlock CreatePromptLabel(string text, Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = ThemeResourceLookup.Brush("InkBrush", Brushes.Black),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
            Margin = margin ?? new Thickness(0, 0, 0, 8),
        };
    }

    private static TextBox CreatePromptTextBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Height = 30,
            Padding = new Thickness(8, 3, 8, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", "Inter, Roboto, Noto Sans, Segoe UI"),
            FontSize = 13,
        };
    }
}
