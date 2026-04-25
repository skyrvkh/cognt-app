using System.Windows;
using System.Windows.Controls;
using CogntApp.Views;

namespace CogntApp.Shell.Overlay;

internal sealed class OverlayHostController
{
    private readonly ContentControl overlayHost;
    private readonly EditorView editorView;

    internal OverlayHostController(ContentControl overlayHost, EditorView editorView)
    {
        ArgumentNullException.ThrowIfNull(overlayHost);
        ArgumentNullException.ThrowIfNull(editorView);

        this.overlayHost = overlayHost;
        this.editorView = editorView;
    }

    internal void Show(UIElement overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        editorView.DetachBlockSuiteHost();
        editorView.Visibility = Visibility.Visible;
        editorView.IsEnabled = true;
        editorView.IsHitTestVisible = true;
        overlayHost.Content = overlay;
        overlayHost.Visibility = Visibility.Visible;
        overlayHost.IsHitTestVisible = true;
    }

    internal void Hide()
    {
        overlayHost.Content = null;
        overlayHost.Visibility = Visibility.Collapsed;
        overlayHost.IsHitTestVisible = false;
        editorView.Visibility = Visibility.Visible;
        editorView.IsEnabled = true;
        editorView.IsHitTestVisible = true;
    }

    internal void ClearIfContent(UIElement overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        if (ReferenceEquals(overlayHost.Content, overlay))
        {
            Hide();
        }
    }
}
