namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed record NormalizedPoint(
    double X,
    double Y);

public sealed record ImageOcclusionMask(
    string Id,
    string TargetId,
    ImageMaskShape Shape,
    double X,
    double Y,
    double Width,
    double Height,
    double? Cx,
    double? Cy,
    double? Rx,
    double? Ry,
    IReadOnlyList<NormalizedPoint>? Points,
    string? Label,
    bool IsDisabled);

public enum ImageMaskShape
{
    Rect = 0,
    Ellipse = 1,
    Polygon = 2,
}

public enum ImageMaskVisualRole
{
    CurrentTarget = 0,
    SiblingTarget = 1,
    HiddenTarget = 2,
    ResolvedTarget = 3,
    Draft = 4,
    Selected = 5,
    Invalid = 6,
    Disabled = 7,
}

public enum ImageMaskSurfaceMode
{
    Edit = 0,
    Preview = 1,
}

public enum ImageMaskEditorTool
{
    Select = 0,
    DrawRectangle = 1,
    DrawEllipse = 2,
    DrawPolygon = 3,
}
