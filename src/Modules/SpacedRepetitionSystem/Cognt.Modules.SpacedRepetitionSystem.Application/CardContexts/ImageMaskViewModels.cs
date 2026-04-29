namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public readonly record struct ImageMaskViewSize(double Width, double Height);

public readonly record struct ImageMaskViewPoint(double X, double Y);

public readonly record struct ImageMaskViewRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0d || Height <= 0d;
}
