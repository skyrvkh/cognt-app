using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.DocumentImport;
using CogntApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Attachments;

internal sealed class AttachmentWorkflowController
{
    private readonly MainViewModel viewModel;
    private readonly IShellDialogService dialogs;
    private readonly ILogger<AttachmentWorkflowController> logger;
    private readonly ILoggerFactory loggerFactory;

    internal AttachmentWorkflowController(
        MainViewModel viewModel,
        IShellDialogService dialogs,
        ILogger<AttachmentWorkflowController> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.viewModel = viewModel;
        this.dialogs = dialogs;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
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

    internal void ShowDocumentImport(Window owner, IEnumerable<string>? initialPaths = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DocumentImportViewModel importViewModel = new(
            viewModel,
            loggerFactory.CreateLogger<DocumentImportViewModel>());
        string[] initialPathSnapshot = initialPaths?.ToArray() ?? [];
        string? initialCollectionPath = TryGetSingleCollectionImportPath(initialPathSnapshot);

        if (initialCollectionPath is null && initialPathSnapshot.Length > 0)
        {
            importViewModel.AddFiles(initialPathSnapshot);
        }

        DocumentImportWindow dialog = new(importViewModel)
        {
            Owner = owner
        };
        if (initialCollectionPath is not null)
        {
            dialog.Loaded += async (_, _) => await importViewModel.ImportExistingCollectionAsync(initialCollectionPath);
        }

        dialog.ShowDialog();
    }

    private static string? TryGetSingleCollectionImportPath(IReadOnlyList<string> paths)
    {
        if (paths.Count != 1
            || string.IsNullOrWhiteSpace(paths[0])
            || !Directory.Exists(paths[0])
            || !DocumentImportPolicy.IsCollectionFolderPath(paths[0]))
        {
            return null;
        }

        return paths[0].Trim();
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
            logger.LogError(ex, "Attachment drop failed. AttachmentCount={AttachmentCount}", attachmentPaths.Count);
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
            logger.LogError(ex, "Attachment operation failed. Title={Title}", title);
            dialogs.ShowWarning(title, $"{failurePrefix}{ex.Message}");
        }
    }
}
