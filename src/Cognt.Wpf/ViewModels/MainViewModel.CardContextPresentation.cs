using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Features.NoteToCard;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void SynchronizeSelectedNoteToCardPresentationPreset()
    {
        isSynchronizingNoteToCardPresentationPreset = true;
        try
        {
            CardPresentationPolicy? policy = TryResolveCurrentPresentationPolicy();
            SelectedNoteToCardPresentationPreset = policy is null
                ? null
                : NoteToCardPresentationPresets.FirstOrDefault(option => option.Preset == policy.Preset);
            SelectedNoteToCardTextFrontMode = policy is null
                ? null
                : NoteToCardTextFrontModes.FirstOrDefault(option => option.Mode == policy.TextFrontMode);
            SelectedNoteToCardImageFrontMode = policy is null
                ? null
                : NoteToCardImageFrontModes.FirstOrDefault(option => option.Mode == policy.ImageFrontMode);
            SelectedNoteToCardBackRevealMode = policy is null
                ? null
                : NoteToCardBackRevealModes.FirstOrDefault(option => option.Mode == policy.BackRevealMode);
            OnPropertyChanged(nameof(IsCustomNoteToCardPresentationPreset));
        }
        finally
        {
            isSynchronizingNoteToCardPresentationPreset = false;
        }
    }

    private void RefreshNoteToCardPresentationAvailability()
    {
        IReadOnlyList<CardCandidateReadModel> targets = GetPresentationTargets();
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

        foreach (CardPresentationPresetOptionViewModel option in NoteToCardPresentationPresets)
        {
            if (!hasTargets)
            {
                option.SetAvailability(enabled: false, "Create or select a card candidate.");
                continue;
            }

            if (option.Preset == CardPresentationPreset.Custom)
            {
                option.SetAvailability(enabled: true);
                continue;
            }

            CardPresentationPolicy policy = CardPresentationPolicies.ForPreset(option.Preset);
            bool needsImage = policy.ImageFrontMode != ImageOcclusionFrontMode.None;
            bool canUse = needsImage
                ? allTargetsHaveImageAnchors
                : allTargetsHaveTextAnchors;
            option.SetAvailability(
                canUse,
                needsImage
                    ? SelectedCardCandidate is null
                        ? "All cards in the queue must have image masks."
                        : "Requires an image mask candidate."
                    : SelectedCardCandidate is null
                        ? "All cards in the queue must have text clozes."
                        : "Requires a text cloze candidate.");
        }
    }

    private CardPresentationPolicy? TryResolveCurrentPresentationPolicy()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return null;
        }

        try
        {
            string relativePath = GetRequiredCurrentNoteRelativePath();
            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            IReadOnlyList<CardCandidateReadModel> targets = GetPresentationTargets();
            if (targets.Count == 0)
            {
                return null;
            }

            Dictionary<string, CardContext> contextsById = CreateCardContextLookup(document.Contexts);
            Dictionary<string, Dictionary<string, CardSlot>> slotLookupsByContextId = new(StringComparer.Ordinal);
            CardPresentationPolicy? sharedPolicy = null;

            foreach (CardCandidateReadModel candidate in targets)
            {
                if (!contextsById.TryGetValue(candidate.ContextId, out CardContext? context))
                {
                    return null;
                }

                if (!slotLookupsByContextId.TryGetValue(context.Id, out Dictionary<string, CardSlot>? slotsById))
                {
                    slotsById = CreateCardSlotLookup(context.Slots);
                    slotLookupsByContextId.Add(context.Id, slotsById);
                }

                slotsById.TryGetValue(candidate.SlotId, out CardSlot? slot);
                CardPresentationPolicy? policy = CardPresentationPolicies.Resolve(context, slot);
                if (policy is null)
                {
                    return null;
                }

                if (sharedPolicy is null)
                {
                    sharedPolicy = policy;
                    continue;
                }

                if (!Equals(sharedPolicy, policy))
                {
                    return null;
                }
            }

            return sharedPolicy;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, CardContext> CreateCardContextLookup(IReadOnlyList<CardContext> contexts)
    {
        Dictionary<string, CardContext> lookup = new(contexts.Count, StringComparer.Ordinal);
        foreach (CardContext context in contexts)
        {
            lookup[context.Id] = context;
        }

        return lookup;
    }

    private static Dictionary<string, CardSlot> CreateCardSlotLookup(IReadOnlyList<CardSlot> slots)
    {
        Dictionary<string, CardSlot> lookup = new(slots.Count, StringComparer.Ordinal);
        foreach (CardSlot slot in slots)
        {
            lookup[slot.Id] = slot;
        }

        return lookup;
    }

    private void ApplySelectedNoteToCardPresentationPreset(CardPresentationPreset? preset)
    {
        if (preset is null || string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        CardPresentationPresetOptionViewModel? selectedOption = NoteToCardPresentationPresets.FirstOrDefault(option =>
            option.Preset == preset.Value);
        if (selectedOption is { IsEnabled: false })
        {
            SynchronizeSelectedNoteToCardPresentationPreset();
            return;
        }

        CardPresentationPolicy policy = preset.Value == CardPresentationPreset.Custom
            ? CreateSelectedCustomPresentationPolicy()
            : CardPresentationPolicies.ForPreset(preset.Value);
        ApplySelectedNoteToCardPresentationPolicy(policy);
    }

    private void ApplySelectedNoteToCardCustomPresentation()
    {
        if (!IsCustomNoteToCardPresentationPreset)
        {
            return;
        }

        ApplySelectedNoteToCardPresentationPolicy(CreateSelectedCustomPresentationPolicy());
    }

    private CardPresentationPolicy CreateSelectedCustomPresentationPolicy()
    {
        return new CardPresentationPolicy(
            CardPresentationPreset.Custom,
            SelectedNoteToCardTextFrontMode?.Mode ?? CardPresentationPolicies.AppDefault.TextFrontMode,
            SelectedNoteToCardImageFrontMode?.Mode ?? CardPresentationPolicies.AppDefault.ImageFrontMode,
            SelectedNoteToCardBackRevealMode?.Mode ?? CardPresentationPolicies.AppDefault.BackRevealMode,
            CueCurrentTarget: true,
            RevealSiblingAnswersOnBack: false);
    }

    private void ApplySelectedNoteToCardPresentationPolicy(CardPresentationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        try
        {
            CardCandidateReadModel? selected = SelectedCardCandidate;
            IReadOnlyList<CardCandidateReadModel> targets = GetPresentationTargets();
            if (targets.Count == 0)
            {
                return;
            }

            string relativePath = GetRequiredCurrentNoteRelativePath();
            string noteContent = GetCurrentEditorContent?.Invoke() ?? lastSavedContent;
            SaveNoteIfChanged(noteContent);

            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            HashSet<string> targetContextIds = targets
                .Select(candidate => candidate.ContextId)
                .ToHashSet(StringComparer.Ordinal);

            CardContext[] updatedContexts = document.Contexts
                .Select(context =>
                {
                    if (!targetContextIds.Contains(context.Id))
                    {
                        return context;
                    }

                    if (selected is not null)
                    {
                        if (!string.Equals(context.Id, selected.ContextId, StringComparison.Ordinal))
                        {
                            return context;
                        }

                        bool changedSelectedSlot = false;
                        CardSlot[] selectedSlots = context.Slots
                            .Select(slot =>
                            {
                                if (!string.Equals(slot.Id, selected.SlotId, StringComparison.Ordinal))
                                {
                                    return slot;
                                }

                                changedSelectedSlot = !Equals(slot.PresentationOverride ?? context.DefaultPresentationPolicy, policy)
                                    || !Equals(slot.PresentationOverride, policy);
                                return slot with { PresentationOverride = policy };
                            })
                            .ToArray();

                        return changedSelectedSlot
                            ? context with { Slots = selectedSlots }
                            : context;
                    }

                    bool alreadyApplied = Equals(context.DefaultPresentationPolicy, policy)
                        && context.TextCueMode is null
                        && context.ImageCueMode is null
                        && context.Slots.All(slot => slot.PresentationOverride is null);
                    if (alreadyApplied)
                    {
                        return context;
                    }

                    CardSlot[] updatedSlots = context.Slots
                        .Select(slot => slot with { PresentationOverride = null })
                        .ToArray();
                    return context with
                    {
                        DefaultPresentationPolicy = policy,
                        Slots = updatedSlots,
                        TextCueMode = null,
                        ImageCueMode = null,
                    };
                })
                .ToArray();

            if (document.Contexts.SequenceEqual(updatedContexts))
            {
                return;
            }

            PersistCardContextDraftDocument(relativePath, noteContent, document with { Contexts = updatedContexts });
            ReloadCurrentNoteToCardQueue(relativePath, noteContent);
            if (selected is not null)
            {
                SelectedCardCandidate = FindCurrentNoteCardCandidate(selected.ContextId, selected.SlotId);
            }
            else
            {
                SelectedCardCandidate = null;
                SynchronizeSelectedNoteToCardPresentationPreset();
            }
        }
        catch (Exception ex)
        {
            RaiseMessage("Card Presentation", ex.Message);
            SynchronizeSelectedNoteToCardPresentationPreset();
        }
    }

    private IReadOnlyList<CardCandidateReadModel> GetPresentationTargets()
    {
        return SelectedCardCandidate is not null
            ? [SelectedCardCandidate]
            : VisibleNoteCardCandidates;
    }

    private static string FormatTextFrontMode(TextClozeFrontMode mode)
    {
        return mode switch
        {
            TextClozeFrontMode.None => "None",
            TextClozeFrontMode.HideCurrentShowSiblings => "Hide current, show siblings",
            TextClozeFrontMode.HideAllCueCurrent => "Hide all, cue current",
            TextClozeFrontMode.HideAllSameStyle => "Hide all same style",
            TextClozeFrontMode.ShowAll => "Show all",
            _ => mode.ToString(),
        };
    }

    private static string FormatImageFrontMode(ImageOcclusionFrontMode mode)
    {
        return mode switch
        {
            ImageOcclusionFrontMode.None => "None",
            ImageOcclusionFrontMode.HideCurrentMaskOnly => "Hide current mask only",
            ImageOcclusionFrontMode.HideAllMasksCueCurrent => "Hide all masks, cue current",
            ImageOcclusionFrontMode.HideAllMasksSameStyle => "Hide all masks same style",
            ImageOcclusionFrontMode.ShowVisibleMasksAsOutlines => "Show visible masks as outlines",
            ImageOcclusionFrontMode.ShowNoMasks => "Show no masks",
            _ => mode.ToString(),
        };
    }

    private static string FormatBackRevealMode(CardBackRevealMode mode)
    {
        return mode switch
        {
            CardBackRevealMode.RevealCurrentTargetOnly => "Reveal current target only",
            CardBackRevealMode.RevealCurrentSlotOnly => "Reveal current slot only",
            CardBackRevealMode.RevealAllTargets => "Reveal all targets",
            CardBackRevealMode.RevealAllSlots => "Reveal all slots",
            _ => mode.ToString(),
        };
    }
}
