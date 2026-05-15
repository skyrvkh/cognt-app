using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CogntApp.ViewModels;

namespace CogntApp.Views;

public partial class NoteGraphToolView : UserControl
{
    public NoteGraphToolView()
    {
        InitializeComponent();
    }

    private void GraphFitButton_Click(object sender, RoutedEventArgs e)
    {
        GraphSurface.FitToGraph();
    }

    private void GraphSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        GraphSettingsPopup.IsOpen = true;
    }

    private void GraphSettingsPopup_Closed(object? sender, EventArgs e)
    {
        SaveGraphSettings();
    }

    private void GraphSettingsTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveGraphSettings();
            GraphSettingsPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            GraphSettingsPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void SaveGraphSettings()
    {
        if (DataContext is GraphViewModel viewModel
            && viewModel.SaveSettingsCommand.CanExecute(null))
        {
            viewModel.SaveSettingsCommand.Execute(null);
        }
    }
}
