using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CogntApp.Features.NoteToCard;
using CogntApp.Shell.Workspace;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private void ViewModel_CardCandidatePreviewRequested(object? sender, CardCandidatePreviewRequestedEventArgs e)
    {
        CardCandidatePreviewWindow previewWindow = new(
            e.Preview)
        {
            Owner = this,
        };

        previewWindow.ShowDialog();
    }

    private async void ViewModel_AddSelectedTextToSelectedCardSlotRequested(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.AddSelectedTextToSelectedCardSlotAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Add selected text to card slot failed. NotePath={NotePath}", session.NotePath);
            shellDialogs.ShowWarning("Card Slot", $"The selected text could not be added to the card: {ex.Message}");
        }
    }

    private async void NoteCardQueueView_CreateClozeRequested(object? sender, EventArgs e)
    {
        try
        {
            await CreateClozeFromActiveEditorAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Create cloze request failed.");
            shellDialogs.ShowWarning("Create Cloze", $"A cloze could not be created: {ex.Message}");
        }
    }

    private async void NoteCardQueueView_OpenImageOcclusionRequested(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.OpenImageOcclusionForSelectedImageAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Open image occlusion request failed. NotePath={NotePath}", session.NotePath);
            shellDialogs.ShowWarning("Image Occlusion", $"Image occlusion could not be opened: {ex.Message}");
        }
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused() || e.Handled)
        {
            return;
        }

        Key key = e.Key == Key.System
            ? e.SystemKey
            : e.Key;

        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Z)
        {
            if (GetActiveNoteDocument() is { } altClozeSession)
            {
                e.Handled = true;
                try
                {
                    await altClozeSession.CreateClozeFromSelectionAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Alt+Z cloze shortcut failed for note. NotePath={NotePath}", altClozeSession.NotePath);
                    shellDialogs.ShowWarning("Create Cloze", $"A cloze could not be created: {ex.Message}");
                }

                return;
            }

            if (GetActiveFlashcardDocument() is { } altFlashcardSession)
            {
                e.Handled = true;
                try
                {
                    await altFlashcardSession.CreateClozeFromSelectionAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Alt+Z cloze shortcut failed for flashcard. CardId={CardId}", altFlashcardSession.CardId);
                    shellDialogs.ShowWarning("Create Cloze", $"A cloze could not be created: {ex.Message}");
                }
            }

            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return;
        }

        switch (key)
        {
            case Key.C:
                if (GetActiveNoteDocument() is { } clozeSession)
                {
                    e.Handled = true;
                    try
                    {
                        await clozeSession.CreateClozeFromSelectionAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ctrl+Shift+C cloze shortcut failed for note. NotePath={NotePath}", clozeSession.NotePath);
                        shellDialogs.ShowWarning("Create Cloze", $"A cloze could not be created: {ex.Message}");
                    }

                    return;
                }

                if (GetActiveFlashcardDocument() is { } flashcardClozeSession)
                {
                    e.Handled = true;
                    try
                    {
                        await flashcardClozeSession.CreateClozeFromSelectionAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ctrl+Shift+C cloze shortcut failed for flashcard. CardId={CardId}", flashcardClozeSession.CardId);
                        shellDialogs.ShowWarning("Create Cloze", $"A cloze could not be created: {ex.Message}");
                    }
                }

                return;
            case Key.I:
                if (GetActiveNoteDocument() is { } imageSession)
                {
                    e.Handled = true;
                    try
                    {
                        await imageSession.OpenImageOcclusionForSelectedImageAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ctrl+Shift+I image occlusion shortcut failed. NotePath={NotePath}", imageSession.NotePath);
                        shellDialogs.ShowWarning("Image Occlusion", $"Image occlusion could not be opened: {ex.Message}");
                    }
                }

                return;
        }
    }

    private async Task CreateClozeFromActiveEditorAsync()
    {
        if (GetActiveNoteDocument() is { } noteSession)
        {
            await noteSession.CreateClozeFromSelectionAsync();
            return;
        }

        if (GetActiveFlashcardDocument() is { } flashcardSession)
        {
            await flashcardSession.CreateClozeFromSelectionAsync();
        }
    }

    private FlashcardWorkspaceDocumentSession? GetActiveFlashcardDocument()
    {
        return openFlashcardDocuments.Values.FirstOrDefault(candidate =>
            !candidate.IsClosed
            && candidate.HasMountedEditor
            && (candidate.LayoutDocument.IsActive
                || candidate.LayoutDocument.IsSelected
                || candidate.OwnsDockedContent(WorkspaceDockingManager.ActiveContent)));
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or PasswordBox;
    }
}
