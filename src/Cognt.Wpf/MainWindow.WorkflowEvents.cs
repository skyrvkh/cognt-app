using System.IO;
using System.Windows;
using CogntApp.Models.Decks;
using CogntApp.Models.Navigation;
using CogntApp.Shell.Dialogs;
using CogntApp.Shell.Editor;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OverlayRequested(object? sender, OverlayRequestedEventArgs e)
    {
        try
        {
            editorOpenRequestId++;
            CloseSidebarSheet();

            switch (e.ViewKind)
            {
                case OverlayViewKind.Decks:
                    await CloseReviewIfActiveAsync();
                    ShowOverlay(decksView);
                    return;

                case OverlayViewKind.AllNotes:
                    await CloseReviewIfActiveAsync();
                    await CloseCreateCardEditorIfActiveAsync();
                    await allNotesViewModel.LoadAsync(viewModel.OpenCollectionPath);
                    ShowOverlay(allNotesView);
                    return;

                case OverlayViewKind.Review:
                    try
                    {
                        await CloseCreateCardEditorIfActiveAsync();
                        await ShowReviewOverlayAsync(EnsureReviewView());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Review mode could not start: {ex.Message}",
                            "Review Not Ready",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        await CloseReviewIfActiveAsync();
                        ShowOverlay(decksView);
                    }

                    return;

                default:
                    await CloseReviewIfActiveAsync();
                    await CloseCreateCardEditorIfActiveAsync();
                    ShowOverlay(dashboardView);
                    return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The view could not be changed: {ex.Message}",
                "Navigation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EditorViewControl.AttachBlockSuiteHostToEditor();
        }
    }

    private async void DecksView_PracticeRequested(object? sender, EventArgs e)
    {
        int requestId = ++editorOpenRequestId;

        try
        {
            viewModel.SaveCurrentNoteFromEditor();
            ReviewView review = EnsureReviewView();
            await CloseCreateCardEditorIfActiveAsync();
            await ShowReviewOverlayAsync(review);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"Review mode could not start: {ex.Message}",
                    "Review Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                await CloseReviewIfActiveAsync();
                ShowOverlay(decksView);
            }
        }
    }

    private async void DecksView_CreateCardEditorHostRequested(
        object? sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        int requestId = ++editorOpenRequestId;

        try
        {
            await CloseReviewIfActiveAsync();
            await EditorViewControl.EditTransientNoteInHostAsync(
                e.Host,
                "Card Text",
                e.Content,
                e.ContentChanged);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The card editor could not start: {ex.Message}",
                    "Card Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private async void DecksView_CreateCardEditorReleased(object? sender, EventArgs e)
    {
        try
        {
            await CloseCreateCardEditorIfActiveAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Card editor restore failed: {ex}");
            EditorViewControl.AttachBlockSuiteHostToEditor();
        }
    }

    private async void ReviewView_ExitReviewRequested(object? sender, EventArgs e)
    {
        try
        {
            editorOpenRequestId++;
            await CloseReviewIfActiveAsync();
            ShowOverlay(decksView);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Review mode could not close cleanly: {ex.Message}",
                "Review",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EditorViewControl.AttachBlockSuiteHostToEditor();
            ShowOverlay(decksView);
        }
    }

    private async void ViewModel_ClearEditorRequested(object? sender, EventArgs e)
    {
        await CreateEditorRequestController().HandleClearEditorRequestedAsync();
    }

    private async void ViewModel_OpenNoteRequested(object? sender, OpenNoteRequestedEventArgs e)
    {
        await CreateEditorRequestController().HandleOpenNoteRequestedAsync(e);
    }

    private void ViewModel_MentionTargetsChanged(object? sender, EventArgs e)
    {
        CreateEditorRequestController().HandleMentionTargetsChanged();
    }

    private void ViewModel_UserMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        CreateEditorRequestController().HandleUserMessageRequested(e);
    }

    private async void AllNotesViewModel_OpenNoteRequested(object? sender, OpenAllNotesRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(viewModel.OpenCollectionPath))
        {
            return;
        }

        string notePath = Path.GetFullPath(Path.Combine(
            viewModel.OpenCollectionPath,
            e.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        await OpenNoteAsync(notePath, selectInTree: true);
    }

    private void AllNotesViewModel_CreateNoteRequested(object? sender, EventArgs e)
    {
        if (viewModel.AddNoteCommand.CanExecute(null))
        {
            viewModel.AddNoteCommand.Execute(null);
        }
    }

    private void AllNotesViewModel_ImportDocumentsRequested(object? sender, EventArgs e)
    {
        ImportDocumentsButton_Click(sender ?? this, new RoutedEventArgs());
    }

    private void ViewModel_CurrentNoteTitleChanged(object? sender, string title)
    {
        CreateEditorRequestController().HandleCurrentNoteTitleChanged(title);
    }

    private EditorRequestController CreateEditorRequestController()
    {
        return new EditorRequestController(
            ShowEmptyEditorViewAsync,
            OpenNoteAsync,
            QueueMentionTargetsRefresh,
            EditorViewControl.UpdateCurrentNoteTitle,
            new WindowShellDialogService(this));
    }

    private async Task<bool> OpenNoteAsync(string notePath, bool selectInTree)
    {
        if (IsPdfFilePath(notePath))
        {
            return await OpenPdfAsync(notePath, selectInTree);
        }

        PreparedNote? note;

        try
        {
            note = viewModel.PrepareNoteForOpen(notePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The note could not be opened: {ex.Message}",
                "Open Note Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (note is null)
        {
            return true;
        }

        int requestId = ++editorOpenRequestId;
        await CloseReviewIfActiveAsync();

        try
        {
            EditorViewControl.AttachBlockSuiteHostToEditor();
            await EditorViewControl.LoadNoteAsync(
                note.Title,
                note.Content,
                note.NotePath,
                viewModel.CurrentCollectionPath);
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The editor could not open this note: {ex.Message}",
                    "Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }

        if (requestId != editorOpenRequestId)
        {
            return false;
        }

        viewModel.CompleteOpenNote(note);
        HideOverlay();

        if (selectInTree)
        {
            SelectNote(note.NotePath);
        }

        return true;
    }

    private async Task<bool> OpenPdfAsync(string documentPath, bool selectInTree)
    {
        try
        {
            viewModel.SaveCurrentNoteFromEditor();
            int requestId = ++editorOpenRequestId;
            await CloseReviewIfActiveAsync();
            ShowOverlay(pdfReaderView);
            await pdfReaderView.LoadPdfFileAsync(documentPath);

            if (requestId != editorOpenRequestId)
            {
                return false;
            }

            if (selectInTree)
            {
                SelectNote(documentPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The PDF could not be opened: {ex.Message}",
                "Open PDF Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task ShowEmptyEditorViewAsync()
    {
        int requestId = ++editorOpenRequestId;
        await CloseReviewIfActiveAsync();
        HideOverlay();

        try
        {
            EditorViewControl.AttachBlockSuiteHostToEditor();
            await EditorViewControl.ClearNoteAsync();
        }
        catch (Exception ex)
        {
            if (requestId == editorOpenRequestId)
            {
                MessageBox.Show(
                    $"The editor could not be cleared: {ex.Message}",
                    "Editor Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
