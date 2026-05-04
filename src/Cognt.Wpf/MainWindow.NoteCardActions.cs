using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CogntApp.Features.NoteToCard;

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
        await editorWorkflow.AddSelectedTextToSelectedCardSlotAsync();
    }

    private async void ViewModel_CreateCardContextFromSelectionRequested(object? sender, EventArgs e)
    {
        await editorWorkflow.CreateCardContextFromSelectionAsync();
    }

    private async void CreateCardContextFromSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        await editorWorkflow.CreateCardContextFromSelectionAsync();
    }

    private async void OpenImageOcclusionEditorButton_Click(object sender, RoutedEventArgs e)
    {
        await editorWorkflow.OpenImageOcclusionForSelectedImageAsync();
    }

    private async void CreateClozeFromSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        await editorWorkflow.CreateClozeFromSelectionAsync();
    }

    private async void NoteCardQueueView_CreateClozeRequested(object? sender, EventArgs e)
    {
        await editorWorkflow.CreateClozeFromSelectionAsync();
    }

    private async void NoteCardQueueView_OpenImageOcclusionRequested(object? sender, EventArgs e)
    {
        await editorWorkflow.OpenImageOcclusionForSelectedImageAsync();
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused()
            || e.Handled
            || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.C:
                e.Handled = true;
                await editorWorkflow.CreateClozeFromSelectionAsync();
                return;
            case Key.I:
                e.Handled = true;
                await editorWorkflow.OpenImageOcclusionForSelectedImageAsync();
                return;
        }
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or PasswordBox;
    }
}
