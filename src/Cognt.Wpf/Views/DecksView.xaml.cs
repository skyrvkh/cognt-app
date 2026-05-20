using System.Windows.Controls;
using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views.Decks;

namespace CogntApp.Views;

public partial class DecksView : UserControl
{
    internal event EventHandler? PracticeRequested;

    internal event EventHandler<CramRequestedEventArgs>? CramRequested;

    internal event EventHandler<CreateCardEditorHostRequestedEventArgs>? CreateCardEditorHostRequested;

    internal event EventHandler? CreateCardEditorReleased;

    internal event EventHandler<UserMessageRequestedEventArgs>? InfoMessageRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    public DecksView(DecksViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        viewModel.InfoMessageRequested += ViewModel_InfoMessageRequested;
        viewModel.WarningMessageRequested += ViewModel_WarningMessageRequested;
    }

    private DecksViewModel ViewModel => (DecksViewModel)DataContext;

    internal void SetCollectionContext(
        string collectionId,
        string? sourceRelPath,
        string? sourceLabel)
    {
        ViewModel.SetCollectionContext(collectionId, sourceRelPath, sourceLabel);
    }

    private void PracticeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        PracticeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CramButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CramSettingsDialog dialog = new()
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true || dialog.SelectedSettings is null)
        {
            return;
        }

        CramRequested?.Invoke(
            this,
            new CramRequestedEventArgs(ViewModel.CreateCramScope(), dialog.SelectedSettings));
    }

    private void CreateCardReviewSubview_CardEditorHostRequested(
        object sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        if (sender is CreateCardReviewSubview)
        {
            CreateCardEditorHostRequested?.Invoke(this, e);
        }
    }

    private void CreateCardReviewSubview_CardEditorReleased(object sender, EventArgs e)
    {
        if (sender is CreateCardReviewSubview)
        {
            CreateCardEditorReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void CreateCardReviewSubview_CreateCardsRequested(
        object sender,
        CreateCardsRequestedEventArgs e)
    {
        if (sender is not CreateCardReviewSubview)
        {
            return;
        }

        await ViewModel.HandleCreateCardsRequestedAsync(e);
    }

    private void EditCardView_CardEditorHostRequested(
        object sender,
        CreateCardEditorHostRequestedEventArgs e)
    {
        if (sender is EditCardView)
        {
            CreateCardEditorHostRequested?.Invoke(this, e);
        }
    }

    private void EditCardView_CardEditorReleased(object sender, EventArgs e)
    {
        if (sender is EditCardView)
        {
            CreateCardEditorReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void EditCardView_SaveCardRequested(
        object sender,
        SaveCardRequestedEventArgs e)
    {
        if (sender is not EditCardView)
        {
            return;
        }

        await ViewModel.HandleSaveCardRequestedAsync(e);
    }

    private void ViewModel_InfoMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        InfoMessageRequested?.Invoke(this, e);
    }

    private void ViewModel_WarningMessageRequested(object? sender, UserMessageRequestedEventArgs e)
    {
        WarningMessageRequested?.Invoke(this, e);
    }
}

internal sealed class CramRequestedEventArgs : EventArgs
{
    internal CramRequestedEventArgs(CramScope scope, CrammingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        Scope = scope;
        Settings = settings;
    }

    internal CramScope Scope { get; }

    internal CrammingSettings Settings { get; }
}
