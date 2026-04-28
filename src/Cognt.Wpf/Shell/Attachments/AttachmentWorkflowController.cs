using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.DocumentImport;
using CogntApp.ViewModels;

namespace CogntApp.Shell.Attachments;

internal sealed class AttachmentWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;

    internal AttachmentWorkflowController(
        MainViewModel viewModel,
        IShellDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
    }

    internal void ShowImportMenu(Button? button)
    {
        if (button is not { ContextMenu: not null })
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    internal void ShowDocumentImport(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DocumentImportWindow dialog = new(new DocumentImportViewModel(viewModel))
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    internal async Task ImportAttachmentAsync(Window owner, string importKind)
    {
        ArgumentNullException.ThrowIfNull(owner);

        OpenFileDialog dialog = new()
        {
            Title = $"Import {importKind}",
            Filter = AttachmentImportPolicy.GetDialogFilter(importKind),
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        await RunAttachmentOperationAsync(
            () => ImportSelectedPathAsync(dialog.FileName, importKind),
            "Import Failed",
            $"The attachment could not be imported:{Environment.NewLine}");
    }

    internal void HandleDragOver(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Effects = AttachmentImportPolicy.GetDroppedAttachmentPaths(e.Data).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    internal async Task HandleDropAsync(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        IReadOnlyList<string> attachmentPaths = AttachmentImportPolicy.GetDroppedAttachmentPaths(e.Data);
        if (attachmentPaths.Count == 0)
        {
            dialogs.ShowInfo("Attach File", "Only supported PDFs, documents, and images can be attached.");
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        try
        {
            await AttachDroppedPathsAsync(attachmentPaths);
            e.Effects = DragDropEffects.Copy;
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Attach File Failed", $"The attachment could not be attached:{Environment.NewLine}{ex.Message}");
            e.Effects = DragDropEffects.None;
        }
        finally
        {
            e.Handled = true;
        }
    }

    private async Task ImportSelectedPathAsync(string path, string importKind)
    {
        if (string.Equals(importKind, AttachmentImportPolicy.PdfImportKind, StringComparison.OrdinalIgnoreCase))
        {
            await viewModel.ImportPdfDocumentAsync(path);
            return;
        }

        await viewModel.ImportAttachmentAsync(path);
    }

    private async Task AttachDroppedPathsAsync(IReadOnlyList<string> attachmentPaths)
    {
        foreach (string attachmentPath in attachmentPaths)
        {
            await viewModel.AttachAttachmentToCurrentNoteAsync(attachmentPath);
        }
    }

    private async Task RunAttachmentOperationAsync(
        Func<Task> operation,
        string title,
        string failurePrefix)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(failurePrefix);

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
        }
    }
}
