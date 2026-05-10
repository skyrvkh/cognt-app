using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Wpf.EditorHost.Abstractions;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Views.Decks;

public partial class CreateCardReviewSubview : UserControl, ITransientEditorHost
{
    private const double HeaderActionsWrapBreakpoint = 720;
    private const double DetailsStackBreakpoint = 900;

    private double lastLayoutWidth = double.NaN;
    private bool hasRequestedTextEditor;
    private string currentCardContent = string.Empty;

    internal event EventHandler<CreateCardEditorHostRequestedEventArgs>? CardEditorHostRequested;

    internal event EventHandler? CardEditorReleased;

    internal event EventHandler<CreateCardsRequestedEventArgs>? CreateCardsRequested;

    public CreateCardReviewSubview()
    {
        InitializeComponent();
    }

    private CreateCardReviewSubviewViewModel? ViewModel => DataContext as CreateCardReviewSubviewViewModel;

    private void CreateCardReviewSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        InitializeCardContent();
        RequestCardTextEditor();
    }

    private void CreateCardReviewSubview_Unloaded(object sender, RoutedEventArgs e)
    {
        hasRequestedTextEditor = false;
        CardTextEditorHost.Content = null;
        CardEditorReleased?.Invoke(this, EventArgs.Empty);
    }

    private void CreateCardReviewSubview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void CreateCards_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        CreateCardsRequested?.Invoke(this, viewModel.BuildCreateRequest(currentCardContent));
    }

    private void RequestCardTextEditor()
    {
        if (hasRequestedTextEditor)
        {
            return;
        }

        hasRequestedTextEditor = true;

        CardEditorHostRequested?.Invoke(
            this,
            new CreateCardEditorHostRequestedEventArgs(
                this,
                currentCardContent,
                UpdateCardContent));
    }

    public void Mount(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        CardTextEditorHost.Content = element;
    }

    public void PrepareForInput()
    {
        CardTextEditorHost.Focusable = true;
        CardTextEditorHost.IsEnabled = true;
        CardTextEditorHost.IsHitTestVisible = true;
    }

    public void UpdateHostLayout()
    {
        CardTextEditorHost.UpdateLayout();
    }

    public void FocusHost()
    {
        CardTextEditorHost.Focus();
    }

    public void SetFocusedElement(IInputElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        FocusManager.SetFocusedElement(CardTextEditorHost, element);
    }

    private void InitializeCardContent()
    {
        if (!string.IsNullOrEmpty(currentCardContent))
        {
            return;
        }

        currentCardContent = ViewModel?.InitialCardContent
            ?? throw new InvalidOperationException("Create card review requires a view model.");
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        bool wrapHeaderActions = lastLayoutWidth < HeaderActionsWrapBreakpoint;
        bool stackDetails = lastLayoutWidth < DetailsStackBreakpoint;

        ResponsiveLayout.ApplyWrappingRow(
            ActionButtonsPanel,
            HeaderActionsSpacerRow,
            wrapHeaderActions,
            ResponsiveLayout.ActionWrappedMargin,
            ResponsiveLayout.ActionInlineMargin);

        DetailsWrapSpacerRow.Height = stackDetails ? ResponsiveLayout.Auto : ResponsiveLayout.Zero;
        ResponsiveLayout.Place(DeckField, 0, 0);
        ResponsiveLayout.Place(
            TagsField,
            stackDetails ? 2 : 0,
            stackDetails ? 0 : 2,
            stackDetails ? 3 : 1);
        ResponsiveLayout.Place(
            SourceField,
            stackDetails ? 2 : 0,
            stackDetails ? 4 : 4);

        DeckField.Margin = stackDetails ? new Thickness(0, 0, 16, 0) : new Thickness(0);
        TagsField.Margin = stackDetails ? new Thickness(0, 0, 16, 12) : new Thickness(0);
        SourceField.Margin = new Thickness(0);
    }

    private void UpdateCardContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        currentCardContent = content;
    }

    private void TagInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.CommitDelimitedTags();
        }
    }

    private void TagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (viewModel.TryHandlePendingTagShortcut(e.Key))
        {
            e.Handled = true;
        }
    }
}
