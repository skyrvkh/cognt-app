using System.Windows.Controls;
using System.Windows;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views.Decks;
using Microsoft.Extensions.Logging;

namespace CogntApp.Views;

public partial class DecksView : UserControl
{

    internal event EventHandler? PracticeRequested;

    internal event EventHandler<CramRequestedEventArgs>? CramRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? InfoMessageRequested;

    internal event EventHandler<UserMessageRequestedEventArgs>? WarningMessageRequested;

    public DecksView(
        DecksViewModel viewModel,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        EditCardDialog.ConfigureLogging(loggerFactory);
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

    internal void SetCramEnabled(bool isEnabled)
    {
        ViewModel.SetCramEnabled(isEnabled);
    }

    internal void ShowCardsForSource(
        string collectionId,
        string sourceRelPath,
        string sourceLabel)
    {
        ViewModel.ShowCardsForSource(collectionId, sourceRelPath, sourceLabel);
    }

    internal async Task OpenCardEditorAsync(
        string collectionId,
        long cardId,
        string? sourceRelPath,
        string sourceLabel)
    {
        if (!string.IsNullOrWhiteSpace(sourceRelPath))
        {
            ViewModel.ShowCardsForSource(collectionId, sourceRelPath, sourceLabel);
        }
        else
        {
            ViewModel.SetCollectionContext(collectionId, sourceRelPath: null, sourceLabel: null);
        }

        CardLibraryRow? card = ViewModel.Cards.CardLibraryRows.FirstOrDefault(row => row.Id == cardId);
        if (card is null)
        {
            WarningMessageRequested?.Invoke(
                this,
                new UserMessageRequestedEventArgs("Card Not Found", "The selected flashcard could not be loaded."));
            return;
        }

        EditCardDialog dialog = new(ViewModel.Cards.CreateEditorView, card, ViewModel.Cards.GetDeckNames())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.EditResult is not null)
        {
            await ViewModel.Cards.UpdateCardContentAsync(card, dialog.EditResult);
        }
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
