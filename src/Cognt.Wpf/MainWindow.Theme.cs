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
        return EditorThemeFactory.Create(viewModel.SelectedThemeOption.Theme);
    }

    private void ApplyShellTheme()
    {
        ApplyWorkspaceDockTheme();
        ApplyWindowTitleBarTheme();
        ApplyThemeToEditors();
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
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedThemeOption))
        {
            ApplyShellTheme();
        }
    }
}
