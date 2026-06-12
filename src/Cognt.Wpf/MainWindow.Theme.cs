using System.ComponentModel;
using AvalonDock.Themes;
using Cognt.Wpf.EditorHost.Models.Editor;
using CogntApp.Design;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels;

namespace CogntApp;

public partial class MainWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyShellTheme();
    }

    private EditorTheme CreateEditorTheme()
    {
        return EditorThemeFactory.Create(viewModel.CreateEditorSettingsSnapshot());
    }

    private void ApplyShellTheme()
    {
        ApplyShellTypography();
        ApplyWorkspaceDockTheme();
        ApplyWindowTitleBarTheme();
        ApplyThemeToEditors();
    }

    private void ApplyShellTypography()
    {
        EditorSettings settings = viewModel.CreateEditorSettingsSnapshot();
        FontFamily = BundledFontFamilyCatalog.CreateFontFamily(settings.InterfaceFontFamily);
        FontSize = settings.GlobalTextFontSize;
    }

    private void ApplyWindowTitleBarTheme()
    {
        WindowTitleBarTheme.Apply(this, viewModel.SelectedThemeOption.Theme);
    }

    private void ApplyWorkspaceDockTheme()
    {
        WorkspaceDockingManager.Theme = viewModel.SelectedThemeOption.Theme == ApplicationTheme.Graphite
            ? new ArcDarkTheme()
            : new ArcLightTheme();
    }

    private void ApplyThemeToEditors()
    {
        EditorTheme theme = CreateEditorTheme();
        EditorViewControl.ApplyTheme(theme);

        foreach (NoteWorkspaceDocumentSession session in openNoteDocuments.Values.ToArray())
        {
            session.ApplyTheme(theme);
        }

        foreach (FlashcardWorkspaceDocumentSession session in openFlashcardDocuments.Values.ToArray())
        {
            session.ApplyTheme(theme);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsCramEnabled))
        {
            decksView.SetCramEnabled(viewModel.IsCramEnabled);
        }

        if (e.PropertyName == nameof(MainViewModel.OpenCollectionPath))
        {
            QueueActiveCollectionViewRefresh();
            _ = rightPaneBrowserView?.UseCollectionAsync(viewModel.OpenCollectionPath);
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentNoteToCardQueue)
            && viewModel.CurrentNoteToCardQueue is null)
        {
            AutoHideNoteCardQueuePane();
        }

        if (IsThemeOrTypographyProperty(e.PropertyName))
        {
            ApplyShellTheme();
        }
    }

    private static bool IsThemeOrTypographyProperty(string? propertyName)
    {
        return propertyName is nameof(MainViewModel.SelectedThemeOption)
            or nameof(MainViewModel.GlobalTextFontSize)
            or nameof(MainViewModel.InterfaceFontFamilyText)
            or nameof(MainViewModel.DisplayFontFamilyText)
            or nameof(MainViewModel.ReadingFontFamilyText)
            or nameof(MainViewModel.CodeFontFamilyText)
            or nameof(MainViewModel.EditorTextFontSizeText)
            or nameof(MainViewModel.EditorHeadingFontSizeText)
            or nameof(MainViewModel.EditorCodeFontSizeText)
            or nameof(MainViewModel.EditorZoomPercentText);
    }
}
