using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CogntApp.Features.NoteToCard;
using CogntApp.Shell.Workspace;

namespace CogntApp;

public partial class MainWindow
{
    private void ViewModel_CardCandidatePreviewRequested(object? sender, CardCandidatePreviewRequestedEventArgs e)
    {
        CardCandidatePreviewWindow previewWindow = new(e.Preview)
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

        await session.AddSelectedTextToSelectedCardSlotAsync();
    }

    private async void ViewModel_CreateCardContextFromSelectionRequested(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        await session.CreateCardContextFromSelectionAsync();
    }

    private async void NoteCardQueueView_CreateClozeRequested(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        await session.CreateClozeFromSelectionAsync();
    }

    private async void NoteCardQueueView_OpenImageOcclusionRequested(object? sender, EventArgs e)
    {
        NoteWorkspaceDocumentSession? session = GetActiveNoteDocument();
        if (session is null)
        {
            return;
        }

        await session.OpenImageOcclusionForSelectedImageAsync();
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
            e.Handled = true;
            if (GetActiveNoteDocument() is { } altClozeSession)
            {
                await altClozeSession.CreateClozeFromSelectionAsync();
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
                e.Handled = true;
                if (GetActiveNoteDocument() is { } clozeSession)
                {
                    await clozeSession.CreateClozeFromSelectionAsync();
                }
                return;
            case Key.I:
                e.Handled = true;
                if (GetActiveNoteDocument() is { } imageSession)
                {
                    await imageSession.OpenImageOcclusionForSelectedImageAsync();
                }
                return;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or PasswordBox;
    }
}
