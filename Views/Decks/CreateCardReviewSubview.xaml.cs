using System.Windows;
using System.Windows.Controls;
using CogntApp.Models.Decks;
using CogntApp.Services;

namespace CogntApp.Views.Decks;

public partial class CreateCardReviewSubview : UserControl
{
    private const string InitialCardText =
        "The {{c1::mitochondria}} is the organelle responsible for producing most of the cell's {{c2::ATP}}.";

    private double lastLayoutWidth = double.NaN;
    private bool hasRequestedTextEditor;
    private string currentCardContent = BlockSuiteDocumentContent.FromPlainText(InitialCardText, "Card Text");

    public event EventHandler<CreateCardEditorHostRequestedEventArgs>? CardEditorHostRequested;

    public event EventHandler? CardEditorReleased;

    public CreateCardReviewSubview()
    {
        InitializeComponent();
    }

    private void CreateCardReviewSubview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
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
                CardTextEditorHost,
                currentCardContent,
                UpdateCardContent));
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginWidthLayout(ActualWidth, ref lastLayoutWidth))
        {
            return;
        }

        bool wrapHeaderActions = lastLayoutWidth < 720;
        bool stackDetails = lastLayoutWidth < 820;

        ResponsiveLayout.ApplyWrappingRow(
            ActionButtonsPanel,
            HeaderActionsSpacerRow,
            wrapHeaderActions,
            new Thickness(0, 12, 0, 0),
            new Thickness(12, 0, 0, 0));

        DetailsWrapSpacerRow.Height = stackDetails ? ResponsiveLayout.Auto : ResponsiveLayout.Zero;
        ResponsiveLayout.Place(NoteTypeField, 0, 0);
        ResponsiveLayout.Place(
            DeckField,
            stackDetails ? 2 : 0,
            stackDetails ? 0 : 2);
        ResponsiveLayout.Place(
            TagsField,
            stackDetails ? 2 : 0,
            stackDetails ? 2 : 4,
            stackDetails ? 3 : 1);

        DeckField.Margin = stackDetails ? new Thickness(0, 0, 16, 0) : new Thickness(0);
        TagsField.Margin = new Thickness(0);
    }

    private void UpdateCardContent(string content)
    {
        currentCardContent = content;
    }
}
