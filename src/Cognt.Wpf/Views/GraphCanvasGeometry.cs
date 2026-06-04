using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Views;

internal static class GraphCanvasGeometry
{
    internal static double GetInitialRingRadius(KnowledgeGraphReadModel graph, int depth)
    {
        return Math.Max(
            34.0d,
            (52.0d * Math.Max(1, depth) + 6.0d * Math.Max(0, depth - 1)) * GetLayoutScale(graph.Nodes.Count));
    }

    internal static double GetTargetRadius(KnowledgeGraphReadModel graph, int depth)
    {
        return Math.Max(34.0d, depth * 48.0d * GetLayoutScale(graph.Nodes.Count));
    }

    internal static double GetIdealDistance(KnowledgeGraphReadModel graph)
    {
        return 44.0d * GetLayoutScale(graph.Nodes.Count);
    }

    internal static double GetMaxAutoFitZoom(bool isMiniView, int nodeCount)
    {
        if (isMiniView)
        {
            if (nodeCount <= 4)
            {
                return 1.05d;
            }

            if (nodeCount <= 8)
            {
                return 1.18d;
            }

            if (nodeCount <= 16)
            {
                return 1.36d;
            }

            return 1.65d;
        }

        if (nodeCount <= 4)
        {
            return 1.35d;
        }

        if (nodeCount <= 8)
        {
            return 1.55d;
        }

        if (nodeCount <= 16)
        {
            return 1.85d;
        }

        return 2.2d;
    }

    internal static Vector ClampVector(Vector vector, double maxLength)
    {
        if (vector.Length <= maxLength)
        {
            return vector;
        }

        vector.Normalize();
        return vector * maxLength;
    }

    internal static Vector CreateStableDirection(string firstDocumentId, string secondDocumentId)
    {
        int hash = StableHash($"{firstDocumentId}|{secondDocumentId}");
        double angle = (hash % 360) * Math.PI / 180.0d;
        return new Vector(Math.Cos(angle), Math.Sin(angle));
    }

    internal static Point GetQuadraticPoint(Point start, Point control, Point end, double progress)
    {
        double inverse = 1.0d - progress;
        return new Point(
            inverse * inverse * start.X + 2.0d * inverse * progress * control.X + progress * progress * end.X,
            inverse * inverse * start.Y + 2.0d * inverse * progress * control.Y + progress * progress * end.Y);
    }

    internal static double GetDistanceToSegment(Point point, Point start, Point end)
    {
        Vector segment = end - start;
        double lengthSquared = Dot(segment, segment);
        if (lengthSquared < 0.001d)
        {
            return (point - start).Length;
        }

        double progress = Math.Clamp(Dot(point - start, segment) / lengthSquared, 0.0d, 1.0d);
        Point closest = start + segment * progress;
        return (point - closest).Length;
    }

    internal static double Dot(Vector left, Vector right)
    {
        return left.X * right.X + left.Y * right.Y;
    }

    internal static string TrimLabel(string label)
    {
        string trimmed = label.Trim();
        return trimmed.Length <= 28
            ? trimmed
            : $"{trimmed[..25]}...";
    }

    internal static string CreateEdgeKey(string sourceDocumentId, string targetDocumentId)
    {
        return string.Join('|', sourceDocumentId.ToUpperInvariant(), targetDocumentId.ToUpperInvariant());
    }

    private static double GetLayoutScale(int nodeCount)
    {
        if (nodeCount <= 4)
        {
            return 0.68d;
        }

        if (nodeCount <= 8)
        {
            return 0.78d;
        }

        if (nodeCount <= 16)
        {
            return 0.90d;
        }

        return 1.05d;
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in text)
            {
                hash = hash * 31 + char.ToUpperInvariant(character);
            }

            return hash & int.MaxValue;
        }
    }
}
