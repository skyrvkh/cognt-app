using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CogntApp.Views;

namespace CogntApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly EditorView editorView = new();
    private readonly DecksView decksView = new();
    private readonly ReviewView reviewView = new();

    public MainWindow()
    {
        InitializeComponent();
        ShowEditorView();
    }

    private void NotesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ShowEditorView();
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        ViewHost.Content = reviewView;
    }

    private void DecksButton_Click(object sender, RoutedEventArgs e)
    {
        ViewHost.Content = decksView;
    }

    private void ShowEditorView()
    {
        ViewHost.Content = editorView;
    }
}
