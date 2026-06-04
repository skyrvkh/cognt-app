using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels;

namespace CogntApp.Views;

public partial class GraphView : UserControl
{
    public GraphView()
    {
        InitializeComponent();
        GraphSurface.NodePreviewRequested += GraphSurface_NodePreviewRequested;
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
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

    private void GraphSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || DataContext is not GraphViewModel viewModel
            || !viewModel.FocusSearchResultCommand.CanExecute(null))
        {
            return;
        }

        viewModel.FocusSearchResultCommand.Execute(null);
        GraphSearchPopup.IsOpen = false;
        e.Handled = true;
    }

    private void GraphSearchResultButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not KnowledgeGraphNodeReadModel node
            || DataContext is not GraphViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedSearchResult = node;
        GraphSearchPopup.IsOpen = false;
    }

    private void GraphSurface_NodePreviewRequested(object? sender, GraphNodePreviewRequestedEventArgs e)
    {
        if (DataContext is GraphViewModel viewModel)
        {
            e.PreviewText = viewModel.CreatePreviewText(e.Node);
            return;
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
