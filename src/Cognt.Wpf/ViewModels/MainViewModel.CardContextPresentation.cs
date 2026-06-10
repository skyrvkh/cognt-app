using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Features.NoteToCard;
using Microsoft.Extensions.Logging;

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
        IReadOnlyList<NoteToCardPresentationPresetAvailability> availability =
            NoteToCardPresentationAvailabilityBuilder.Create(
                NoteToCardPresentationPresets.Select(option => option.Preset),
                targets,
                SelectedCardCandidate is not null);

        for (int index = 0; index < NoteToCardPresentationPresets.Count; index++)
        {
            NoteToCardPresentationPresetAvailability presetAvailability = availability[index];
            NoteToCardPresentationPresets[index].SetAvailability(
                presetAvailability.IsEnabled,
                presetAvailability.DisabledReason);
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
            CardPresentationPolicy? policy = NoteToCardPresentationPolicyResolver.ResolveSharedPolicy(document, targets);
            return policy;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(
                ex,
                "Current note-to-card presentation policy resolution failed. CollectionPath={CollectionPath} CurrentNotePath={CurrentNotePath}",
                openCollectionPath,
                currentNotePath);
            return null;
        }
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
            NoteToCardPresentationPolicyApplication application =
                NoteToCardPresentationPolicyApplicator.Apply(document, targets, selected, policy);
            if (!application.HasChanges)
            {
                return;
            }

            PersistCardContextDraftDocument(relativePath, noteContent, application.Document);
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
            CardCandidateReadModel? selected = SelectedCardCandidate;
            logger?.LogError(
                ex,
                "Apply note-to-card presentation policy failed. CollectionPath={CollectionPath} CurrentNotePath={CurrentNotePath} Preset={Preset} CandidateContextId={CandidateContextId} CandidateSlotId={CandidateSlotId}",
                openCollectionPath,
                currentNotePath,
                policy.Preset,
                selected?.ContextId,
                selected?.SlotId);
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
