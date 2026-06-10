using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CogntApp.Design;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CogntApp.Shell.Dialogs;

internal sealed partial class WindowShellDialogService(Window owner, ILogger<WindowShellDialogService> dialogLogger) : IShellDialogService
{
    private readonly Window ownerWindow = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly ILogger<WindowShellDialogService> logger = dialogLogger ?? throw new ArgumentNullException(nameof(dialogLogger));

    internal WindowShellDialogService(Window owner)
        : this(owner, NullLogger<WindowShellDialogService>.Instance)
    {
    }

    public void ShowInfo(string title, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ShowMessageBox(title, message, MessageBoxImage.Information);
    }

    public void ShowWarning(string title, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ShowMessageBox(title, message, MessageBoxImage.Warning);
    }

    public bool ConfirmWarning(string title, string message)
    {
        EnsureMessageDialogArgs(title, message);

        bool confirmed = MessageBox.Show(ownerWindow, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        return confirmed;
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
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", EditorSettings.DefaultInterfaceFontFamily),
            FontSize = 13,
            Owner = ownerWindow,
            Content = content,
        };
    }

    private static Button CreateDialogActionButton(string label, bool isDefault = false, bool isCancel = false, Thickness? margin = null)
    {
        Button button = new()
        {
            Content = label,
            Width = 76,
            Height = 28,
            Margin = margin ?? default,
            IsDefault = isDefault,
            IsCancel = isCancel,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", EditorSettings.DefaultInterfaceFontFamily),
            FontSize = 13,
        };

        button.Style = ThemeResourceLookup.Style(isDefault ? "PrimaryButtonStyle" : "OutlineButtonStyle");
        ApplyButtonContentTextResources(button);
        return button;
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

    private static TextBlock CreateDialogLabel(
        string text,
        Brush foreground,
        double fontSize,
        Thickness? margin = null,
        FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", EditorSettings.DefaultInterfaceFontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            Margin = margin ?? new Thickness(0, 0, 0, 8),
        };
    }

    private static TextBox CreateDialogTextBox(
        string text,
        double height,
        Thickness padding,
        double fontSize,
        Brush? background = null,
        Brush? borderBrush = null,
        Brush? foreground = null)
    {
        return new TextBox
        {
            Text = text,
            Height = height,
            Padding = padding,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = background ?? SystemColors.WindowBrush,
            BorderBrush = borderBrush ?? SystemColors.ActiveBorderBrush,
            Foreground = foreground ?? ThemeResourceLookup.Brush("InkBrush", Brushes.Black),
            BorderThickness = new Thickness(1),
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", EditorSettings.DefaultInterfaceFontFamily),
            FontSize = fontSize,
        };
    }

    private static Button CreateThemedDialogButton(
        string label,
        Brush background,
        Brush borderBrush,
        Brush foreground,
        double height,
        double minWidth,
        Thickness padding,
        double fontSize,
        FontWeight fontWeight,
        bool isDefault = false,
        bool isCancel = false,
        Thickness? margin = null,
        string? styleKey = null)
    {
        Button button = new()
        {
            Content = label,
            Height = height,
            MinWidth = minWidth,
            Padding = padding,
            Margin = margin ?? default,
            Background = background,
            BorderBrush = borderBrush,
            Foreground = foreground,
            BorderThickness = new Thickness(1),
            IsDefault = isDefault,
            IsCancel = isCancel,
            FontFamily = ThemeResourceLookup.FontFamily("AppSansFontFamily", EditorSettings.DefaultInterfaceFontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        button.Style = ThemeResourceLookup.Style(string.IsNullOrWhiteSpace(styleKey) ? "OutlineButtonStyle" : styleKey);
        ApplyButtonContentTextResources(button);
        return button;
    }

    private static void ApplyButtonContentTextResources(Button button)
    {
        button.Resources[typeof(TextBlock)] = ThemeResourceLookup.Style("ButtonTemplateTextForegroundStyle");
        button.Resources[typeof(AccessText)] = ThemeResourceLookup.Style("ButtonTemplateAccessTextForegroundStyle");
    }
}
