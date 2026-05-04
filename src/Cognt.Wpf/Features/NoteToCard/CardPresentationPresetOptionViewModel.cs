using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard;

public sealed record CardPresentationPresetOptionViewModel(
    CardPresentationPreset Preset,
    string Label)
{
    public override string ToString() => Label;
}

public sealed record TextClozeFrontModeOptionViewModel(
    TextClozeFrontMode Mode,
    string Label)
{
    public override string ToString() => Label;
}

public sealed record ImageOcclusionFrontModeOptionViewModel(
    ImageOcclusionFrontMode Mode,
    string Label)
{
    public override string ToString() => Label;
}

public sealed record CardBackRevealModeOptionViewModel(
    CardBackRevealMode Mode,
    string Label)
{
    public override string ToString() => Label;
}
