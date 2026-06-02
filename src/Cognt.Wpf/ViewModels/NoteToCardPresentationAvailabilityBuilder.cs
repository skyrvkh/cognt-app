using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal sealed record NoteToCardPresentationPresetAvailability(
    CardPresentationPreset Preset,
    bool IsEnabled,
    string? DisabledReason);

internal static class NoteToCardPresentationAvailabilityBuilder
{
    internal static IReadOnlyList<NoteToCardPresentationPresetAvailability> Create(
        IEnumerable<CardPresentationPreset> presets,
        IReadOnlyList<CardCandidateReadModel> targets,
        bool hasSelectedCandidate)
    {
        TargetCapabilitySummary summary = CreateTargetCapabilitySummary(targets);
        var availability = new List<NoteToCardPresentationPresetAvailability>();

        foreach (CardPresentationPreset preset in presets)
        {
            availability.Add(CreatePresetAvailability(preset, summary, hasSelectedCandidate));
        }

        return availability;
    }

    private static NoteToCardPresentationPresetAvailability CreatePresetAvailability(
        CardPresentationPreset preset,
        TargetCapabilitySummary summary,
        bool hasSelectedCandidate)
    {
        if (!summary.HasTargets)
        {
            return new(preset, IsEnabled: false, "Create or select a card candidate.");
        }

        if (preset == CardPresentationPreset.Custom)
        {
            return new(preset, IsEnabled: true, DisabledReason: null);
        }

        CardPresentationPolicy policy = CardPresentationPolicies.ForPreset(preset);
        bool needsImage = policy.ImageFrontMode != ImageOcclusionFrontMode.None;
        bool isEnabled = needsImage
            ? summary.AllTargetsHaveImageAnchors
            : summary.AllTargetsHaveTextAnchors;

        return new(
            preset,
            isEnabled,
            isEnabled ? null : CreateDisabledReason(needsImage, hasSelectedCandidate));
    }

    private static TargetCapabilitySummary CreateTargetCapabilitySummary(
        IReadOnlyList<CardCandidateReadModel> targets)
    {
        bool hasTargets = targets.Count > 0;
        bool allTargetsHaveTextAnchors = hasTargets;
        bool allTargetsHaveImageAnchors = hasTargets;

        if (hasTargets)
        {
            foreach (CardCandidateReadModel candidate in targets)
            {
                if (allTargetsHaveTextAnchors && candidate.TextAnchors.Count == 0)
                {
                    allTargetsHaveTextAnchors = false;
                }

                if (allTargetsHaveImageAnchors && candidate.ImageAnchors.Count == 0)
                {
                    allTargetsHaveImageAnchors = false;
                }

                if (!allTargetsHaveTextAnchors && !allTargetsHaveImageAnchors)
                {
                    break;
                }
            }
        }

        return new TargetCapabilitySummary(
            hasTargets,
            allTargetsHaveTextAnchors,
            allTargetsHaveImageAnchors);
    }

    private static string CreateDisabledReason(bool needsImage, bool hasSelectedCandidate)
    {
        return needsImage
            ? hasSelectedCandidate
                ? "Requires an image mask candidate."
                : "All cards in the queue must have image masks."
            : hasSelectedCandidate
                ? "Requires a text cloze candidate."
                : "All cards in the queue must have text clozes.";
    }

    private readonly record struct TargetCapabilitySummary(
        bool HasTargets,
        bool AllTargetsHaveTextAnchors,
        bool AllTargetsHaveImageAnchors);
}
