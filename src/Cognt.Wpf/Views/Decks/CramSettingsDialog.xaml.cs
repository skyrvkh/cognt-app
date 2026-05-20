using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.ViewModels.Decks;

namespace CogntApp.Views.Decks;

public partial class CramSettingsDialog : Window
{
    private readonly CramSettingsDialogViewModel viewModel = new();

    public CramSettingsDialog()
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.StartRequested += ViewModel_StartRequested;
        viewModel.CancelRequested += ViewModel_CancelRequested;
    }

    internal CrammingSettings? SelectedSettings { get; private set; }

    protected override void OnClosed(EventArgs e)
    {
        viewModel.StartRequested -= ViewModel_StartRequested;
        viewModel.CancelRequested -= ViewModel_CancelRequested;
        base.OnClosed(e);
    }

    private void ViewModel_StartRequested(object? sender, CramSettingsAcceptedEventArgs e)
    {
        SelectedSettings = e.Settings;
        DialogResult = true;
    }

    private void ViewModel_CancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
    }
}
