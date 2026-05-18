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

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenFilePicker();
    }

    private void DropZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.HasNoFiles)
        {
            OpenFilePicker();
        }
    }

    private void DropZone_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        OpenFilePicker();
        e.Handled = true;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedPaths(e, out string[] paths) && paths.Any(DocumentImportPolicy.IsSupportedPath)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e, out string[] paths))
        {
            ViewModel.AddFiles(paths);
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OpenFilePicker()
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import files",
            Filter = "Documents (*.docx;*.pdf;*.md)|*.docx;*.pdf;*.md",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.AddFiles(dialog.FileNames);
        }
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
}
