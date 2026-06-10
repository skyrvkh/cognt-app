using System.Windows;
using CogntApp.Shell.DocumentImport;

namespace CogntApp;

public partial class MainWindow
{
    private void MainWindow_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetImportDropPaths(e, out string[] paths))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void MainWindow_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!TryGetImportDropPaths(e, out string[] paths))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        attachmentWorkflow.ShowDocumentImport(this, paths);
    }

    private static bool TryGetImportDropPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths)
        {
            return false;
        }

        List<string> importPaths = new(droppedPaths.Length);
        foreach (string path in droppedPaths)
        {
            if (DocumentImportPolicy.IsSupportedImportDropPath(path))
            {
                importPaths.Add(path);
            }
        }

        paths = [.. importPaths];
        return paths.Length > 0;
    }
}
