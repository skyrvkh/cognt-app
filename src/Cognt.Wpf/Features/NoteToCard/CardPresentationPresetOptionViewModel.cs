using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.ViewModels;

namespace CogntApp.Features.NoteToCard;

public sealed class CardPresentationPresetOptionViewModel : ObservableObject
{
    private bool isEnabled = true;
    private string disabledReason = string.Empty;

    public CardPresentationPresetOptionViewModel(CardPresentationPreset preset, string label)
    {
        Preset = preset;
        Label = label;
    }

    public CardPresentationPreset Preset { get; }

    public string Label { get; }

    public bool IsEnabled
    {
        get => isEnabled;
        private set => SetProperty(ref isEnabled, value);
    }

    public string DisabledReason
    {
        get => disabledReason;
        private set => SetProperty(ref disabledReason, value ?? string.Empty);
    }

    public void SetAvailability(bool enabled, string? reason = null)
    {
        IsEnabled = enabled;
        DisabledReason = enabled ? string.Empty : reason ?? string.Empty;
    }

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
