using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Views;

public sealed class GraphCanvas : FrameworkElement
{
    public static readonly DependencyProperty GraphProperty = DependencyProperty.Register(
        nameof(Graph),
        typeof(KnowledgeGraphReadModel),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OpenNodeCommandProperty = DependencyProperty.Register(
        nameof(OpenNodeCommand),
        typeof(ICommand),
        typeof(GraphCanvas),
        new PropertyMetadata(null));

    public static readonly DependencyProperty FocusedDocumentIdProperty = DependencyProperty.Register(
        nameof(FocusedDocumentId),
        typeof(string),
        typeof(GraphCanvas),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsMiniViewProperty = DependencyProperty.Register(
        nameof(IsMiniView),
        typeof(bool),
        typeof(GraphCanvas),
        new PropertyMetadata(false));

    public event EventHandler<GraphNodePreviewRequestedEventArgs>? NodePreviewRequested;

    public KnowledgeGraphReadModel? Graph
    {
        get => (KnowledgeGraphReadModel?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public ICommand? OpenNodeCommand
    {
        get => (ICommand?)GetValue(OpenNodeCommandProperty);
        set => SetValue(OpenNodeCommandProperty, value);
    }

    public string FocusedDocumentId
    {
        get => (string)GetValue(FocusedDocumentIdProperty);
        set => SetValue(FocusedDocumentIdProperty, value);
    }

    public bool IsMiniView
    {
        get => (bool)GetValue(IsMiniViewProperty);
        set => SetValue(IsMiniViewProperty, value);
    }

    public void FitToGraph()
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 255)), null, new Rect(RenderSize));
    }
}

public sealed class GraphNodePreviewRequestedEventArgs : EventArgs
{
    public GraphNodePreviewRequestedEventArgs(KnowledgeGraphNodeReadModel node)
    {
        Node = node;
    }

    public KnowledgeGraphNodeReadModel Node { get; }

    public string PreviewText { get; set; } = string.Empty;
}
