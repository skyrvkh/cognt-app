using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Design;

namespace CogntApp.Views;

public sealed class GraphCanvas : FrameworkElement
{
    public static readonly DependencyProperty GraphProperty = DependencyProperty.Register(
        nameof(Graph),
        typeof(KnowledgeGraphReadModel),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGraphChanged));

    public static readonly DependencyProperty OpenNodeCommandProperty = DependencyProperty.Register(
        nameof(OpenNodeCommand),
        typeof(ICommand),
        typeof(GraphCanvas),
        new PropertyMetadata(null));

    public static readonly DependencyProperty FocusedDocumentIdProperty = DependencyProperty.Register(
        nameof(FocusedDocumentId),
        typeof(string),
        typeof(GraphCanvas),
        new PropertyMetadata(string.Empty, OnFocusedDocumentIdChanged));

    public static readonly DependencyProperty IsMiniViewProperty = DependencyProperty.Register(
        nameof(IsMiniView),
        typeof(bool),
        typeof(GraphCanvas),
        new PropertyMetadata(false));

    public static readonly DependencyProperty SurfaceBrushProperty = DependencyProperty.Register(
        nameof(SurfaceBrush),
        typeof(Brush),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const int InitialSimulationFrames = 90;
    private const int InteractionSimulationFrames = 240;
    private const double EdgeNodeClearance = 18.0d;
    private const double EdgeNodeForce = 0.16d;

    private readonly Dictionary<string, Point> positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector> velocities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector> simulationForces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KnowledgeGraphNodeReadModel> nodesByDocumentId = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> reciprocalEdgeKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> staleVelocityKeys = [];
    private readonly List<GraphNodeOrderEntry> drawOrderedNodes = [];
    private readonly List<GraphNodeOrderEntry> hitTestOrderedNodes = [];
    private KnowledgeGraphReadModel? layoutGraph;
    private Vector pan;
    private double zoom = 1.0d;
    private bool autoFitPending = true;
    private bool isPanning;
    private bool isDraggingNode;
    private bool isSimulationRunning;
    private Point lastMousePosition;
    private Vector draggedNodeOffset;
    private int activeSimulationFrames;
    private string? hoveredDocumentId;
    private string? selectedDocumentId;
    private string? draggedDocumentId;

    public event EventHandler<GraphNodePreviewRequestedEventArgs>? NodePreviewRequested;

    public GraphCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        SizeChanged += (_, _) =>
        {
            autoFitPending = true;
            InvalidateVisual();
        };
        Loaded += (_, _) => ActivateSimulation(InitialSimulationFrames);
        Unloaded += (_, _) => StopSimulation();
    }

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

    public Brush? SurfaceBrush
    {
        get => (Brush?)GetValue(SurfaceBrushProperty);
        set => SetValue(SurfaceBrushProperty, value);
    }

    public void FitToGraph()
    {
        EnsureLayout();
        FitToGraphCore();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(
            SurfaceBrush ?? GetBrush("GraphCanvasBrush", Color.FromRgb(248, 250, 255)),
            null,
            new Rect(RenderSize));

        KnowledgeGraphReadModel? currentGraph = Graph;
        if (currentGraph is null || currentGraph.Nodes.Count == 0)
        {
            return;
        }

        EnsureLayout();
        if (autoFitPending)
        {
            FitToGraphCore(centerOnCurrentNode: true);
        }

        DrawEdges(drawingContext, currentGraph);
        DrawNodes(drawingContext, currentGraph);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        Focus();
        EnsureLayout();
        lastMousePosition = e.GetPosition(this);

        KnowledgeGraphNodeReadModel? hitNode = HitTestNode(lastMousePosition);
        if (hitNode is not null)
        {
            selectedDocumentId = hitNode.DocumentId;
            if (e.ClickCount >= 2)
            {
                ExecuteOpenNode(hitNode);
                e.Handled = true;
                InvalidateVisual();
                return;
            }

            isDraggingNode = true;
            draggedDocumentId = hitNode.DocumentId;
            draggedNodeOffset = positions.TryGetValue(hitNode.DocumentId, out Point nodePosition)
                ? nodePosition - ScreenToWorld(lastMousePosition)
                : new Vector();
            velocities[hitNode.DocumentId] = new Vector();
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            ActivateSimulation(InteractionSimulationFrames);
        }
        else
        {
            isPanning = true;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Point position = e.GetPosition(this);
        KnowledgeGraphNodeReadModel? hitNode = HitTestNode(position);
        string? nextHoveredDocumentId = hitNode?.DocumentId;
        if (!string.Equals(hoveredDocumentId, nextHoveredDocumentId, StringComparison.OrdinalIgnoreCase))
        {
            hoveredDocumentId = nextHoveredDocumentId;
            ToolTip = hitNode is null
                ? null
                : CreateToolTip(hitNode);
            InvalidateVisual();
        }

        if (isDraggingNode && draggedDocumentId is not null)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndPointerInteraction();
                return;
            }

            positions[draggedDocumentId] = ScreenToWorld(position) + draggedNodeOffset;
            velocities[draggedDocumentId] = new Vector();
            lastMousePosition = position;
            ActivateSimulation(InteractionSimulationFrames);
            InvalidateVisual();
            return;
        }

        if (isPanning && e.LeftButton == MouseButtonState.Pressed)
        {
            Vector delta = position - lastMousePosition;
            pan += delta;
            lastMousePosition = position;
            InvalidateVisual();
            return;
        }

        if (isPanning && e.LeftButton != MouseButtonState.Pressed)
        {
            EndPointerInteraction();
            return;
        }

        Cursor = hitNode is null ? null : Cursors.Hand;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            EndPointerInteraction();
            e.Handled = true;
        }
    }

    private void EndPointerInteraction()
    {
        bool wasDraggingNode = isDraggingNode;
        isPanning = false;
        isDraggingNode = false;
        draggedDocumentId = null;
        draggedNodeOffset = new Vector();
        Cursor = null;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (wasDraggingNode)
        {
            ActivateSimulation(InteractionSimulationFrames);
        }

        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (IsMouseCaptured)
        {
            base.OnMouseLeave(e);
            return;
        }

        hoveredDocumentId = null;
        ToolTip = null;
        Cursor = null;
        InvalidateVisual();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Point cursor = e.GetPosition(this);
        Point worldBeforeZoom = ScreenToWorld(cursor);
        double scale = e.Delta > 0 ? 1.12d : 1.0d / 1.12d;
        zoom = Math.Clamp(zoom * scale, 0.2d, 4.0d);
        pan = new Vector(
            cursor.X - ActualWidth / 2.0d - worldBeforeZoom.X * zoom,
            cursor.Y - ActualHeight / 2.0d - worldBeforeZoom.Y * zoom);
        InvalidateVisual();
        e.Handled = true;
    }

    private static void OnGraphChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        KnowledgeGraphReadModel? newGraph = e.NewValue as KnowledgeGraphReadModel;
        canvas.layoutGraph = null;
        canvas.positions.Clear();
        canvas.velocities.Clear();
        canvas.simulationForces.Clear();
        canvas.nodesByDocumentId.Clear();
        canvas.reciprocalEdgeKeys.Clear();
        canvas.staleVelocityKeys.Clear();
        canvas.drawOrderedNodes.Clear();
        canvas.hitTestOrderedNodes.Clear();
        canvas.hoveredDocumentId = null;
        canvas.selectedDocumentId = newGraph?.CurrentDocumentId;
        canvas.draggedDocumentId = null;
        canvas.isDraggingNode = false;
        canvas.isPanning = false;
        canvas.autoFitPending = true;
        canvas.ActivateSimulation(InitialSimulationFrames);
        canvas.InvalidateVisual();
    }

    private static void OnFocusedDocumentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        string? documentId = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return;
        }

        canvas.FocusNode(documentId);
    }

    private void EnsureLayout()
    {
        KnowledgeGraphReadModel? currentGraph = Graph;
        if (currentGraph is null || ReferenceEquals(layoutGraph, currentGraph))
        {
            return;
        }

        positions.Clear();
        velocities.Clear();
        RefreshNodeLookup(currentGraph);
        BuildInitialLayout(currentGraph);
        RefineLayout(currentGraph);
        layoutGraph = currentGraph;
        autoFitPending = true;
        ActivateSimulation(InitialSimulationFrames);
    }

    private void BuildInitialLayout(KnowledgeGraphReadModel currentGraph)
    {
        Dictionary<int, List<KnowledgeGraphNodeReadModel>> nodesByDepth = [];
        List<int> depths = [];

        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            if (node.IsCurrent)
            {
                positions[node.DocumentId] = new Point(0, 0);
                continue;
            }

            if (!nodesByDepth.TryGetValue(node.Depth, out List<KnowledgeGraphNodeReadModel>? depthNodes))
            {
                depthNodes = [];
                nodesByDepth[node.Depth] = depthNodes;
                depths.Add(node.Depth);
            }

            depthNodes.Add(node);
        }

        depths.Sort();
        foreach (int depth in depths)
        {
            List<KnowledgeGraphNodeReadModel> nodes = nodesByDepth[depth];
            nodes.Sort(CompareNodesByTitleThenPath);
            double radius = GraphCanvasGeometry.GetInitialRingRadius(currentGraph, depth);
            double offset = depth % 2 == 0 ? Math.PI / Math.Max(1, nodes.Count) : 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                double angle = offset + (Math.PI * 2.0d * i / nodes.Count);
                positions[nodes[i].DocumentId] = new Point(
                    Math.Cos(angle) * radius,
                    Math.Sin(angle) * radius);
            }
        }
    }

    private static int CompareNodesByTitleThenPath(
        KnowledgeGraphNodeReadModel left,
        KnowledgeGraphNodeReadModel right)
    {
        int titleComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
        return titleComparison != 0
            ? titleComparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
    }

    private void RefreshNodeLookup(KnowledgeGraphReadModel currentGraph)
    {
        nodesByDocumentId.Clear();
        drawOrderedNodes.Clear();
        hitTestOrderedNodes.Clear();
        for (int index = 0; index < currentGraph.Nodes.Count; index++)
        {
            KnowledgeGraphNodeReadModel node = currentGraph.Nodes[index];
            nodesByDocumentId[node.DocumentId] = node;
            GraphNodeOrderEntry entry = new(node, index);
            drawOrderedNodes.Add(entry);
            hitTestOrderedNodes.Add(entry);
        }

        drawOrderedNodes.Sort(CompareDrawOrder);
        hitTestOrderedNodes.Sort(CompareHitTestOrder);
    }

    private void RefineLayout(KnowledgeGraphReadModel currentGraph)
    {
        if (currentGraph.Nodes.Count <= 1)
        {
            return;
        }

        EnsureVelocityEntries(currentGraph);
        for (int iteration = 0; iteration < 70; iteration++)
        {
            StepSimulation(currentGraph);
        }
    }

    private void ActivateSimulation(int frameBudget)
    {
        activeSimulationFrames = Math.Max(activeSimulationFrames, frameBudget);
        if (isSimulationRunning || !IsLoaded || Graph is not { Nodes.Count: > 1 })
        {
            return;
        }

        CompositionTarget.Rendering += OnSimulationFrame;
        isSimulationRunning = true;
    }

    private void StopSimulation()
    {
        if (!isSimulationRunning)
        {
            return;
        }

        CompositionTarget.Rendering -= OnSimulationFrame;
        isSimulationRunning = false;
    }

    private void OnSimulationFrame(object? sender, EventArgs e)
    {
        KnowledgeGraphReadModel? currentGraph = Graph;
        if (currentGraph is null || currentGraph.Nodes.Count <= 1)
        {
            StopSimulation();
            return;
        }

        EnsureLayout();
        bool moved = StepSimulation(currentGraph);
        if (moved)
        {
            InvalidateVisual();
        }

        if (isDraggingNode)
        {
            activeSimulationFrames = Math.Max(activeSimulationFrames, 2);
            return;
        }

        activeSimulationFrames--;
        if (activeSimulationFrames <= 0)
        {
            StopSimulation();
        }
    }

    private bool StepSimulation(KnowledgeGraphReadModel currentGraph)
    {
        EnsureVelocityEntries(currentGraph);
        double idealDistance = GraphCanvasGeometry.GetIdealDistance(currentGraph);
        simulationForces.Clear();
        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            simulationForces[node.DocumentId] = new Vector();
        }

        for (int i = 0; i < currentGraph.Nodes.Count; i++)
        {
            KnowledgeGraphNodeReadModel a = currentGraph.Nodes[i];
            for (int j = i + 1; j < currentGraph.Nodes.Count; j++)
            {
                KnowledgeGraphNodeReadModel b = currentGraph.Nodes[j];
                if (!positions.TryGetValue(a.DocumentId, out Point aPosition)
                    || !positions.TryGetValue(b.DocumentId, out Point bPosition))
                {
                    continue;
                }

                Vector direction = aPosition - bPosition;
                double distance = direction.Length;
                if (distance < 0.001d)
                {
                    direction = GraphCanvasGeometry.CreateStableDirection(a.DocumentId, b.DocumentId);
                    distance = 1.0d;
                }
                else
                {
                    direction /= distance;
                }

                double effectiveDistance = Math.Max(26.0d, distance);
                double strength = idealDistance * idealDistance / effectiveDistance;
                simulationForces[a.DocumentId] += direction * strength;
                simulationForces[b.DocumentId] -= direction * strength;
            }
        }

        foreach (KnowledgeGraphEdgeReadModel edge in currentGraph.Edges)
        {
            if (!positions.TryGetValue(edge.SourceDocumentId, out Point source)
                || !positions.TryGetValue(edge.TargetDocumentId, out Point target)
                || !simulationForces.ContainsKey(edge.SourceDocumentId)
                || !simulationForces.ContainsKey(edge.TargetDocumentId))
            {
                continue;
            }

            Vector direction = target - source;
            double distance = direction.Length;
            if (distance < 0.001d)
            {
                direction = GraphCanvasGeometry.CreateStableDirection(edge.SourceDocumentId, edge.TargetDocumentId);
                distance = 1.0d;
            }
            else
            {
                direction /= distance;
            }

            double mentionWeight = Math.Min(3.0d, Math.Max(1.0d, edge.MentionCount));
            double targetDistance = idealDistance + 6.0d;
            double strength = (distance - targetDistance) * 0.090d * mentionWeight;
            simulationForces[edge.SourceDocumentId] += direction * strength;
            simulationForces[edge.TargetDocumentId] -= direction * strength;
        }

        ApplyEdgeNodeSeparationForces(currentGraph);

        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            if (!positions.TryGetValue(node.DocumentId, out Point position))
            {
                continue;
            }

            Vector toCenter = new(-position.X, -position.Y);
            double centerStrength = node.IsCurrent ? 0.030d : 0.0035d;
            simulationForces[node.DocumentId] += toCenter * centerStrength;

            if (!node.IsCurrent && toCenter.Length > 0.001d)
            {
                Vector radial = new(position.X, position.Y);
                double radius = radial.Length;
                radial /= radius;
                double targetRadius = GraphCanvasGeometry.GetTargetRadius(currentGraph, node.Depth);
                simulationForces[node.DocumentId] += radial * ((targetRadius - radius) * 0.006d);
            }
        }

        bool moved = false;
        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            if (node.IsCurrent && !string.Equals(node.DocumentId, draggedDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                positions[node.DocumentId] = new Point(0, 0);
                velocities[node.DocumentId] = new Vector();
                continue;
            }

            if (string.Equals(node.DocumentId, draggedDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                velocities[node.DocumentId] = new Vector();
                continue;
            }

            Vector velocity = (velocities[node.DocumentId] + simulationForces[node.DocumentId] * 0.014d) * 0.78d;
            velocity = GraphCanvasGeometry.ClampVector(velocity, 22.0d);
            velocities[node.DocumentId] = velocity;
            positions[node.DocumentId] += velocity;
            moved |= velocity.Length > 0.02d;
        }

        return moved;
    }

    private void ApplyEdgeNodeSeparationForces(KnowledgeGraphReadModel currentGraph)
    {
        foreach (KnowledgeGraphEdgeReadModel edge in currentGraph.Edges)
        {
            if (!positions.TryGetValue(edge.SourceDocumentId, out Point source)
                || !positions.TryGetValue(edge.TargetDocumentId, out Point target)
                || !simulationForces.ContainsKey(edge.SourceDocumentId)
                || !simulationForces.ContainsKey(edge.TargetDocumentId))
            {
                continue;
            }

            Vector edgeVector = target - source;
            double edgeLengthSquared = GraphCanvasGeometry.Dot(edgeVector, edgeVector);
            if (edgeLengthSquared < 1.0d)
            {
                continue;
            }

            foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
            {
                if (string.Equals(node.DocumentId, edge.SourceDocumentId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.DocumentId, edge.TargetDocumentId, StringComparison.OrdinalIgnoreCase)
                    || !positions.TryGetValue(node.DocumentId, out Point nodePosition))
                {
                    continue;
                }

                double progress = Math.Clamp(GraphCanvasGeometry.Dot(nodePosition - source, edgeVector) / edgeLengthSquared, 0.08d, 0.92d);
                Point closestPoint = source + edgeVector * progress;
                Vector away = nodePosition - closestPoint;
                double distance = away.Length;
                if (distance < 0.001d)
                {
                    away = GraphCanvasGeometry.CreateStableDirection(edge.SourceDocumentId, node.DocumentId);
                    distance = 1.0d;
                }
                else
                {
                    away /= distance;
                }

                double clearance = GetNodeRadius(node) + EdgeNodeClearance;
                if (distance >= clearance)
                {
                    continue;
                }

                double strength = (clearance - distance) * EdgeNodeForce;
                simulationForces[node.DocumentId] += away * strength;
                simulationForces[edge.SourceDocumentId] -= away * strength * 0.035d;
                simulationForces[edge.TargetDocumentId] -= away * strength * 0.035d;
            }
        }
    }

    private void EnsureVelocityEntries(KnowledgeGraphReadModel currentGraph)
    {
        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            velocities.TryAdd(node.DocumentId, new Vector());
        }

        staleVelocityKeys.Clear();
        foreach (string documentId in velocities.Keys)
        {
            if (!nodesByDocumentId.ContainsKey(documentId))
            {
                staleVelocityKeys.Add(documentId);
            }
        }

        foreach (string staleDocumentId in staleVelocityKeys)
        {
            velocities.Remove(staleDocumentId);
        }
    }

    private void FitToGraphCore()
    {
        FitToGraphCore(centerOnCurrentNode: false);
    }

    private void FitToGraphCore(bool centerOnCurrentNode)
    {
        autoFitPending = false;
        if (positions.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        GetPositionBounds(out double minX, out double maxX, out double minY, out double maxY);
        minX -= 80;
        maxX += 80;
        minY -= 80;
        maxY += 80;
        double width = Math.Max(1, maxX - minX);
        double height = Math.Max(1, maxY - minY);
        double maxZoom = GraphCanvasGeometry.GetMaxAutoFitZoom(IsMiniView, Graph?.Nodes.Count ?? positions.Count);
        zoom = Math.Clamp(Math.Min(ActualWidth / width, ActualHeight / height), 0.35d, maxZoom);
        Point center = new((minX + maxX) / 2.0d, (minY + maxY) / 2.0d);
        if (centerOnCurrentNode
            && !IsMiniView
            && Graph?.CurrentDocumentId is { Length: > 0 } currentDocumentId
            && positions.TryGetValue(currentDocumentId, out Point currentPosition))
        {
            center = currentPosition;
        }

        pan = new Vector(-center.X * zoom, -center.Y * zoom);
    }

    private void FocusNode(string documentId)
    {
        KnowledgeGraphReadModel? currentGraph = Graph;
        if (currentGraph is null)
        {
            return;
        }

        EnsureLayout();
        if (!nodesByDocumentId.ContainsKey(documentId))
        {
            return;
        }

        if (!positions.TryGetValue(documentId, out Point position))
        {
            return;
        }

        selectedDocumentId = documentId;
        autoFitPending = false;
        pan = new Vector(-position.X * zoom, -position.Y * zoom);
        ActivateSimulation(InteractionSimulationFrames);
        InvalidateVisual();
    }

    private void GetPositionBounds(out double minX, out double maxX, out double minY, out double maxY)
    {
        using Dictionary<string, Point>.ValueCollection.Enumerator enumerator = positions.Values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            minX = maxX = minY = maxY = 0;
            return;
        }

        Point first = enumerator.Current;
        minX = maxX = first.X;
        minY = maxY = first.Y;

        while (enumerator.MoveNext())
        {
            Point point = enumerator.Current;
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }
    }

    private void DrawEdges(DrawingContext drawingContext, KnowledgeGraphReadModel currentGraph)
    {
        reciprocalEdgeKeys.Clear();
        foreach (KnowledgeGraphEdgeReadModel edge in currentGraph.Edges)
        {
            reciprocalEdgeKeys.Add(GraphCanvasGeometry.CreateEdgeKey(edge.TargetDocumentId, edge.SourceDocumentId));
        }

        foreach (KnowledgeGraphEdgeReadModel edge in currentGraph.Edges)
        {
            if (!positions.TryGetValue(edge.SourceDocumentId, out Point sourceWorld)
                || !positions.TryGetValue(edge.TargetDocumentId, out Point targetWorld))
            {
                continue;
            }

            if (!nodesByDocumentId.TryGetValue(edge.SourceDocumentId, out KnowledgeGraphNodeReadModel? sourceNode)
                || !nodesByDocumentId.TryGetValue(edge.TargetDocumentId, out KnowledgeGraphNodeReadModel? targetNode))
            {
                continue;
            }

            Point source = ToScreen(sourceWorld);
            Point target = ToScreen(targetWorld);
            Vector direction = target - source;
            if (direction.Length < 1.0d)
            {
                continue;
            }

            direction.Normalize();
            Vector normal = new(-direction.Y, direction.X);
            bool hasReciprocal = reciprocalEdgeKeys.Contains(GraphCanvasGeometry.CreateEdgeKey(edge.SourceDocumentId, edge.TargetDocumentId));
            double offset = hasReciprocal
                ? string.Compare(edge.SourceDocumentId, edge.TargetDocumentId, StringComparison.OrdinalIgnoreCase) < 0 ? 7.0d : -7.0d
                : 0.0d;

            double sourceRadius = GetNodeRadius(sourceNode) * zoom;
            double targetRadius = GetNodeRadius(targetNode) * zoom;
            Point start = source + direction * sourceRadius + normal * offset;
            Point end = target - direction * (targetRadius + 5.0d) + normal * offset;

            bool highlighted = IsHighlighted(edge.SourceDocumentId) || IsHighlighted(edge.TargetDocumentId);
            Brush edgeBrush = highlighted
                ? GetBrush("GraphEdgeHighlightBrush", Color.FromRgb(49, 86, 212))
                : GetBrush("GraphEdgeBrush", Color.FromRgb(160, 169, 188));
            Pen pen = new(edgeBrush, highlighted ? 2.0d : 1.2d)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            double avoidanceOffset = GetScreenEdgeAvoidanceOffset(currentGraph, edge, start, end, normal);
            Point labelPoint;
            if (Math.Abs(avoidanceOffset) > 0.1d)
            {
                Point control = new Point((start.X + end.X) / 2.0d, (start.Y + end.Y) / 2.0d)
                    + normal * avoidanceOffset;
                DrawQuadraticEdge(drawingContext, pen, start, control, end);

                Vector arrowDirection = end - control;
                if (arrowDirection.Length < 1.0d)
                {
                    arrowDirection = direction;
                }
                else
                {
                    arrowDirection.Normalize();
                }

                DrawArrowHead(drawingContext, end, arrowDirection, edgeBrush, highlighted);
                labelPoint = GraphCanvasGeometry.GetQuadraticPoint(start, control, end, 0.5d);
            }
            else
            {
                drawingContext.DrawLine(pen, start, end);
                DrawArrowHead(drawingContext, end, direction, edgeBrush, highlighted);
                labelPoint = new Point((start.X + end.X) / 2.0d, (start.Y + end.Y) / 2.0d);
            }

            if (edge.MentionCount > 1 && zoom > 0.55d)
            {
                DrawBadge(drawingContext, labelPoint, edge.MentionCount.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private double GetScreenEdgeAvoidanceOffset(
        KnowledgeGraphReadModel currentGraph,
        KnowledgeGraphEdgeReadModel edge,
        Point start,
        Point end,
        Vector normal)
    {
        double largestOffset = 0.0d;
        foreach (KnowledgeGraphNodeReadModel node in currentGraph.Nodes)
        {
            if (string.Equals(node.DocumentId, edge.SourceDocumentId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.DocumentId, edge.TargetDocumentId, StringComparison.OrdinalIgnoreCase)
                || !positions.TryGetValue(node.DocumentId, out Point nodeWorld))
            {
                continue;
            }

            Point nodeCenter = ToScreen(nodeWorld);
            double protectedRadius = GetNodeRadius(node) * zoom + 10.0d;
            double distance = GraphCanvasGeometry.GetDistanceToSegment(nodeCenter, start, end);
            if (distance >= protectedRadius)
            {
                continue;
            }

            double side = GraphCanvasGeometry.Dot(nodeCenter - start, normal) >= 0.0d ? -1.0d : 1.0d;
            double neededOffset = Math.Min(96.0d, protectedRadius - distance + 26.0d);
            if (Math.Abs(neededOffset) > Math.Abs(largestOffset))
            {
                largestOffset = neededOffset * side;
            }
        }

        return largestOffset;
    }

    private static void DrawQuadraticEdge(DrawingContext drawingContext, Pen pen, Point start, Point control, Point end)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.QuadraticBezierTo(control, end, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private void DrawNodes(DrawingContext drawingContext, KnowledgeGraphReadModel currentGraph)
    {
        foreach (GraphNodeOrderEntry entry in drawOrderedNodes)
        {
            KnowledgeGraphNodeReadModel node = entry.Node;
            if (!positions.TryGetValue(node.DocumentId, out Point world))
            {
                continue;
            }

            Point center = ToScreen(world);
            double radius = GetNodeRadius(node) * zoom;
            bool highlighted = IsHighlighted(node.DocumentId);
            Brush fill = GetNodeFill(node, highlighted);
            Pen stroke = new(GetNodeStroke(node, highlighted), highlighted ? 2.4d : 1.4d);
            drawingContext.DrawEllipse(fill, stroke, center, radius, radius);

            if (node.IsCurrent)
            {
                drawingContext.DrawEllipse(
                    null,
                    new Pen(GetBrush("GraphNodeCurrentRingBrush", Color.FromRgb(255, 255, 255)), 2.0d),
                    center,
                    radius - 4.0d,
                    radius - 4.0d);
            }

            if (ShouldDrawNodeLabel(node, highlighted))
            {
                DrawNodeLabel(drawingContext, node, center, radius, GetNodeLabelOpacity(node, highlighted));
            }
        }
    }

    private void DrawNodeLabel(
        DrawingContext drawingContext,
        KnowledgeGraphNodeReadModel node,
        Point center,
        double radius,
        double opacity)
    {
        string text = GraphCanvasGeometry.TrimLabel(node.Title);
        double fontSize = node.IsCurrent ? 13.0d : 12.0d;
        FormattedText formatted = CreateFormattedText(
            text,
            fontSize,
            node.IsCurrent
                ? GetBrush("GraphCurrentNodeLabelForegroundBrush", Color.FromRgb(255, 255, 255))
                : GetBrush("GraphNodeLabelForegroundBrush", Color.FromRgb(31, 36, 51)));
        Point textPoint = new(
            center.X - formatted.Width / 2.0d,
            center.Y + radius + 6.0d);
        Rect background = new(
            textPoint.X - 5.0d,
            textPoint.Y - 2.0d,
            formatted.Width + 10.0d,
            formatted.Height + 4.0d);
        drawingContext.PushOpacity(opacity);
        drawingContext.DrawRoundedRectangle(
            node.IsCurrent
                ? GetBrush("GraphCurrentNodeLabelBackgroundBrush", Color.FromRgb(49, 86, 212))
                : GetBrush("GraphNodeLabelBackgroundBrush", Color.FromArgb(230, 255, 255, 255)),
            new Pen(GetBrush("GraphToolLineBrush", Color.FromArgb(90, 200, 207, 220)), 1.0d),
            background,
            5.0d,
            5.0d);
        drawingContext.DrawText(formatted, textPoint);
        drawingContext.Pop();
    }

    private void DrawBadge(DrawingContext drawingContext, Point center, string text)
    {
        FormattedText formatted = CreateFormattedText(
            text,
            10.0d,
            GetBrush("GraphBadgeForegroundBrush", Color.FromRgb(255, 255, 255)));
        double radius = Math.Max(8.0d, formatted.Width / 2.0d + 5.0d);
        drawingContext.DrawEllipse(
            GetBrush("GraphBadgeBackgroundBrush", Color.FromRgb(97, 105, 126)),
            null,
            center,
            radius,
            8.0d);
        drawingContext.DrawText(formatted, new Point(center.X - formatted.Width / 2.0d, center.Y - formatted.Height / 2.0d));
    }

    private void DrawArrowHead(
        DrawingContext drawingContext,
        Point end,
        Vector direction,
        Brush brush,
        bool highlighted)
    {
        Vector normal = new(-direction.Y, direction.X);
        double length = highlighted ? 10.0d : 8.0d;
        double width = highlighted ? 5.0d : 4.0d;
        Point p1 = end;
        Point p2 = end - direction * length + normal * width;
        Point p3 = end - direction * length - normal * width;

        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(p1, isFilled: true, isClosed: true);
            context.LineTo(p2, isStroked: true, isSmoothJoin: true);
            context.LineTo(p3, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(brush, null, geometry);
    }

    private KnowledgeGraphNodeReadModel? HitTestNode(Point screenPoint)
    {
        if (Graph is null)
        {
            return null;
        }

        foreach (GraphNodeOrderEntry entry in hitTestOrderedNodes)
        {
            KnowledgeGraphNodeReadModel node = entry.Node;
            if (!positions.TryGetValue(node.DocumentId, out Point world))
            {
                continue;
            }

            Point center = ToScreen(world);
            double radius = Math.Max(10.0d, GetNodeRadius(node) * zoom + 4.0d);
            if ((screenPoint - center).Length <= radius)
            {
                return node;
            }
        }

        return null;
    }

    private static int CompareDrawOrder(GraphNodeOrderEntry left, GraphNodeOrderEntry right)
    {
        int depthComparison = right.Node.Depth.CompareTo(left.Node.Depth);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        int titleComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Node.Title, right.Node.Title);
        return titleComparison != 0
            ? titleComparison
            : left.OriginalIndex.CompareTo(right.OriginalIndex);
    }

    private static int CompareHitTestOrder(GraphNodeOrderEntry left, GraphNodeOrderEntry right)
    {
        int depthComparison = left.Node.Depth.CompareTo(right.Node.Depth);
        return depthComparison != 0
            ? depthComparison
            : left.OriginalIndex.CompareTo(right.OriginalIndex);
    }

    private readonly record struct GraphNodeOrderEntry(
        KnowledgeGraphNodeReadModel Node,
        int OriginalIndex);

    private void ExecuteOpenNode(KnowledgeGraphNodeReadModel node)
    {
        ICommand? command = OpenNodeCommand;
        if (command?.CanExecute(node) == true)
        {
            command.Execute(node);
        }
    }

    private string CreateToolTip(KnowledgeGraphNodeReadModel node)
    {
        if (IsMiniView)
        {
            return node.Title;
        }

        GraphNodePreviewRequestedEventArgs args = new(node)
        {
            PreviewText = node.Title,
        };
        NodePreviewRequested?.Invoke(this, args);
        return string.IsNullOrWhiteSpace(args.PreviewText)
            ? node.Title
            : args.PreviewText;
    }

    private bool IsHighlighted(string documentId)
    {
        return string.Equals(documentId, selectedDocumentId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentId, hoveredDocumentId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentId, draggedDocumentId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldDrawNodeLabel(KnowledgeGraphNodeReadModel node, bool highlighted)
    {
        if (node.IsCurrent || highlighted)
        {
            return true;
        }

        double threshold = IsMiniView ? 0.72d : 0.62d;
        if (zoom >= threshold)
        {
            return true;
        }

        return !IsMiniView && node.Depth <= 1 && zoom >= 0.42d;
    }

    private double GetNodeLabelOpacity(KnowledgeGraphNodeReadModel node, bool highlighted)
    {
        if (node.IsCurrent || highlighted)
        {
            return 1.0d;
        }

        if (zoom < 0.50d)
        {
            return 0.24d;
        }

        if (zoom < 0.75d)
        {
            return 0.48d;
        }

        return 0.86d;
    }

    private Point ToScreen(Point world)
    {
        return new Point(
            ActualWidth / 2.0d + pan.X + world.X * zoom,
            ActualHeight / 2.0d + pan.Y + world.Y * zoom);
    }

    private Point ScreenToWorld(Point screen)
    {
        return new Point(
            (screen.X - ActualWidth / 2.0d - pan.X) / zoom,
            (screen.Y - ActualHeight / 2.0d - pan.Y) / zoom);
    }

    private static double GetNodeRadius(KnowledgeGraphNodeReadModel node)
    {
        return node.IsCurrent
            ? 17.0d
            : Math.Max(8.0d, 14.0d - node.Depth * 1.4d);
    }

    private Brush GetNodeFill(KnowledgeGraphNodeReadModel node, bool highlighted)
    {
        if (node.IsCurrent)
        {
            return GetBrush("GraphNodeCurrentFillBrush", Color.FromRgb(49, 86, 212));
        }

        if (highlighted)
        {
            return GetBrush("GraphNodeHighlightFillBrush", Color.FromRgb(255, 245, 225));
        }

        string key = node.Depth switch
        {
            1 => "GraphNodeDepthOneFillBrush",
            2 => "GraphNodeDepthTwoFillBrush",
            _ => "GraphNodeFillBrush",
        };
        Color fallback = node.Depth switch
        {
            1 => Color.FromRgb(218, 245, 235),
            2 => Color.FromRgb(238, 242, 255),
            _ => Color.FromRgb(241, 244, 248),
        };
        return GetBrush(key, fallback);
    }

    private Brush GetNodeStroke(KnowledgeGraphNodeReadModel node, bool highlighted)
    {
        if (highlighted)
        {
            return GetBrush("GraphNodeHighlightStrokeBrush", Color.FromRgb(222, 112, 43));
        }

        if (node.IsCurrent)
        {
            return GetBrush("GraphNodeCurrentStrokeBrush", Color.FromRgb(28, 58, 176));
        }

        string key = node.Depth switch
        {
            1 => "GraphNodeDepthOneStrokeBrush",
            2 => "GraphNodeDepthTwoStrokeBrush",
            _ => "GraphNodeStrokeBrush",
        };
        Color fallback = node.Depth switch
        {
            1 => Color.FromRgb(47, 138, 107),
            2 => Color.FromRgb(86, 105, 192),
            _ => Color.FromRgb(105, 112, 132),
        };
        return GetBrush(key, fallback);
    }

    private Brush GetBrush(string resourceKey, Color fallback)
    {
        return TryFindResource(resourceKey) as Brush
            ?? new SolidColorBrush(fallback);
    }

    private FormattedText CreateFormattedText(string text, double fontSize, Brush brush)
    {
        FontFamily fontFamily = TryFindResource("AppSansFontFamily") as FontFamily
            ?? BundledFontFamilyCatalog.CreateFontFamily(EditorSettings.DefaultInterfaceFontFamily);

        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
