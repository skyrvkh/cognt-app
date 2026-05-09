using System.Windows;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public Task ClearNoteAsync()
    {
        ++loadRequestId;
        ClearTransientEditorState();
        currentContent = string.Empty;
        currentTitle = string.Empty;
        currentSourcePath = null;
        currentCollectionRoot = null;
        ShowNoNoteState();
        return Task.CompletedTask;
    }

    public void UpdateCurrentNoteTitle(string title)
    {
        currentTitle = title;
    }

    public string GetContent()
    {
        return currentContent;
    }

    public void ShowLoadingState(string title, string message)
    {
        ShowEditorState(title, message, canRetry: false, showEditor: false);
    }

    public void ShowFailedState(string message, bool canRetry)
    {
        ShowEditorState("Failed to load editor", message, canRetry, showEditor: false);
    }

    public void ShowNoNoteState()
    {
        ShowEditorState("No note selected", "Select a note to view it here.", canRetry: false, showEditor: false);
    }

    private void ShowReadyState()
    {
        EditorStateOverlay.Visibility = Visibility.Collapsed;
        SetBlockSuiteHostVisible(true);
    }

    private void ShowEditorState(string title, string message, bool canRetry, bool showEditor)
    {
        EditorStateTitleText.Text = title;
        EditorStateMessageText.Text = message;
        EditorRetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        EditorStateOverlay.Visibility = Visibility.Visible;
        SetBlockSuiteHostVisible(showEditor);
    }

    private void EditorRetryButton_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}
