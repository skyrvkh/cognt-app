using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void SynchronizeSelectedNoteToCardPresentationPreset()
    {
        isSynchronizingNoteToCardPresentationPreset = true;
        try
        {
            CardPresentationPolicy? policy = TryResolveSelectedPresentationPolicy();
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

    private CardPresentationPolicy? TryResolveSelectedPresentationPolicy()
    {
        if (SelectedCardCandidate is null || string.IsNullOrWhiteSpace(currentNotePath))
        {
            return null;
        }

        try
        {
            string relativePath = GetRequiredCurrentNoteRelativePath();
            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            CardContext? context = document.Contexts.FirstOrDefault(item =>
                string.Equals(item.Id, SelectedCardCandidate.ContextId, StringComparison.Ordinal));
            CardSlot? slot = context?.Slots.FirstOrDefault(item =>
                string.Equals(item.Id, SelectedCardCandidate.SlotId, StringComparison.Ordinal));
            return context is null
                ? null
                : CardPresentationPolicies.Resolve(context, slot);
        }
        catch
        {
            return null;
        }
    }

    private void ApplySelectedNoteToCardPresentationPreset(CardPresentationPreset? preset)
    {
        if (preset is null || SelectedCardCandidate is null || string.IsNullOrWhiteSpace(currentNotePath))
        {
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
        if (SelectedCardCandidate is null || string.IsNullOrWhiteSpace(currentNotePath))
        {
            return;
        }

        try
        {
            CardCandidateReadModel selected = SelectedCardCandidate;
            string relativePath = GetRequiredCurrentNoteRelativePath();
            string noteContent = GetCurrentEditorContent?.Invoke() ?? lastSavedContent;
            SaveNoteIfChanged(noteContent);

            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            CardContext context = document.Contexts.FirstOrDefault(item =>
                    string.Equals(item.Id, selected.ContextId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The selected card context no longer exists.");
            CardSlot? selectedSlot = context.Slots.FirstOrDefault(slot =>
                string.Equals(slot.Id, selected.SlotId, StringComparison.Ordinal));
            if (selectedSlot is not null
                && Equals(CardPresentationPolicies.Resolve(context, selectedSlot), policy)
                && Equals(selectedSlot.PresentationOverride ?? context.DefaultPresentationPolicy, policy))
            {
                return;
            }

            CardSlot[] updatedSlots = context.Slots
                .Select(slot => string.Equals(slot.Id, selected.SlotId, StringComparison.Ordinal)
                    && slot.PresentationOverride is not null
                        ? slot with { PresentationOverride = policy }
                        : slot)
                .ToArray();
            context = context with
            {
                DefaultPresentationPolicy = policy,
                Slots = updatedSlots,
                TextCueMode = null,
                ImageCueMode = null,
            };

            PersistCardContextDraftDocument(relativePath, noteContent, ReplaceDraftContext(document, context));
            ReloadCurrentNoteToCardQueue(relativePath, noteContent);
            SelectedCardCandidate = CurrentNoteToCardQueue?.Items.FirstOrDefault(item =>
                string.Equals(item.ContextId, selected.ContextId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, selected.SlotId, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            RaiseMessage("Card Presentation", ex.Message);
            SynchronizeSelectedNoteToCardPresentationPreset();
        }
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
