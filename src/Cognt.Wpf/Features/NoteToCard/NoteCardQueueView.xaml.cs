using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CogntApp.Features.NoteToCard;

public partial class NoteCardQueueView : UserControl
{
    public NoteCardQueueView()
    {
        InitializeComponent();
    }

    public event EventHandler? CreateClozeRequested;

    public event EventHandler? OpenImageOcclusionRequested;

    private void CreateClozeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CreateClozeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenImageOcclusionButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenImageOcclusionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CardCandidatesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        ListBoxItem? item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is null || !item.IsSelected)
        {
            return;
        }

        listBox.SelectedItem = null;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = GetParentObject(source);
        }

        return null;
    }

    private static DependencyObject? GetParentObject(DependencyObject source)
    {
        if (source is Visual || source is Visual3D)
        {
            return VisualTreeHelper.GetParent(source);
        }

        if (source is FrameworkContentElement frameworkContentElement)
        {
            return frameworkContentElement.Parent ?? ContentOperations.GetParent(frameworkContentElement);
        }

        if (source is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement);
        }

        return null;
    }
}
