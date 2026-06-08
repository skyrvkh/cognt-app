using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace CogntApp.Shell.DocumentImport;

public partial class DocumentImportWindow : Window
{
    internal DocumentImportWindow(DocumentImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    private DocumentImportViewModel ViewModel => (DocumentImportViewModel)DataContext;

    private async void ImportSourceButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await OpenImportPickerAsync();
    }

    private async void DropZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.HasNoFiles)
        {
            await OpenImportPickerAsync();
        }
    }

    private async void DropZone_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        await OpenImportPickerAsync();
        e.Handled = true;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        bool hasPaths = TryGetDroppedPaths(e, out string[] paths);
        bool hasSupportedPath = hasPaths && ContainsSupportedPath(paths);
        e.Effects = hasSupportedPath
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e, out string[] paths))
        {
            if (TryGetSingleCollectionFolderPath(paths, out string? collectionPath))
            {
                await ViewModel.ImportExistingCollectionAsync(collectionPath);
            }
            else
            {
                ViewModel.AddFiles(paths);
            }

            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private async Task OpenImportPickerAsync()
    {
        if (!ViewModel.CanImportCollection || TryOpenFilePicker())
        {
            return;
        }

        await TryOpenFolderPickerAsync();
    }

    private bool TryOpenFilePicker()
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import files",
            Filter = "Documents (*.docx;*.pdf;*.md)|*.docx;*.pdf;*.md",
            InitialDirectory = GetDefaultFolderDialogPath(),
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.AddFiles(dialog.FileNames);
            return true;
        }

        return false;
    }

    private async Task TryOpenFolderPickerAsync()
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Import folder or Cognt collection",
            InitialDirectory = GetDefaultFolderDialogPath(),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string selectedPath = dialog.FolderName.Trim();
        if (DocumentImportPolicy.IsCollectionFolderPath(selectedPath))
        {
            await ViewModel.ImportExistingCollectionAsync(selectedPath);
            return;
        }

        ViewModel.AddFiles([selectedPath]);
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths)
        {
            return false;
        }

        paths = droppedPaths;
        return paths.Length > 0;
    }

    private static bool ContainsSupportedPath(string[] paths)
    {
        foreach (string path in paths)
        {
            if (DocumentImportPolicy.IsSupportedImportDropPath(path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSingleCollectionFolderPath(string[] paths, out string collectionPath)
    {
        collectionPath = string.Empty;
        if (paths.Length != 1
            || string.IsNullOrWhiteSpace(paths[0])
            || !Directory.Exists(paths[0])
            || !DocumentImportPolicy.IsCollectionFolderPath(paths[0]))
        {
            return false;
        }

        collectionPath = paths[0].Trim();
        return true;
    }

    private string GetDefaultFolderDialogPath()
    {
        string defaultPath = Directory.Exists(ViewModel.DefaultCollectionParentPath)
            ? ViewModel.DefaultCollectionParentPath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return defaultPath;
    }
}
