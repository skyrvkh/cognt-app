using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognt.Wpf.EditorHost.Abstractions;
using CogntApp.ViewModels.Decks;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.Views.Decks;

public partial class EditCardView : UserControl, ITransientEditorHost
{
    private const double HeaderActionsWrapBreakpoint = 720;

    private double lastLayoutWidth = double.NaN;
    private bool hasRequestedTextEditor;
    private string currentCardContent = string.Empty;

    internal event EventHandler<CreateCardEditorHostRequestedEventArgs>? CardEditorHostRequested;

    internal event EventHandler? CardEditorReleased;

    internal event EventHandler<SaveCardRequestedEventArgs>? SaveCardRequested;

    public EditCardView()
    {
        InitializeComponent();
    }

    private EditCardViewModel? ViewModel => DataContext as EditCardViewModel;

    private void EditCardView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        InitializeCardContent();
        RequestCardTextEditor();
    }

    private void EditCardView_Unloaded(object sender, RoutedEventArgs e)
    {
        hasRequestedTextEditor = false;
        CardTextEditorHost.Content = null;
        CardEditorReleased?.Invoke(this, EventArgs.Empty);
    }

    private void EditCardView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void InitializeCardContent()
    {
        if (ViewModel is { } viewModel)
        {
            currentCardContent = viewModel.FrontContent;
        }
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

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        bool wrapHeaderActions = lastLayoutWidth < HeaderActionsWrapBreakpoint;
        ResponsiveLayout.ApplyWrappingRow(
            ActionButtonsPanel,
            HeaderActionsSpacerRow,
            wrapHeaderActions,
            ResponsiveLayout.ActionWrappedMargin,
            ResponsiveLayout.ActionInlineMargin);
    }

    private void UpdateCardContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        currentCardContent = content;
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { CanSaveChanges: true } viewModel)
        {
            return;
        }

        SaveCardRequested?.Invoke(this, viewModel.BuildSaveRequest(currentCardContent));
    }
}
