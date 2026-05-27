using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.ViewModels;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskEditorViewModel
{
    private void RunEditorMutation(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        PushUndoSnapshot();
        redoSnapshots.Clear();
        action();
        IsDirty = true;
        UpdateCommandStates();
    }

    private void PushUndoSnapshot()
    {
        undoSnapshots.Push(CreateSnapshot());
        UpdateCommandStates();
    }

    private EditorSnapshot CreateSnapshot()
    {
        return new EditorSnapshot(
            currentContext,
            selectedMaskId,
            selectedMaskLabelText,
            selectedMaskAnswerText,
            selectedMaskHintText,
            selectedTargetOption?.TargetId);
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        currentContext = snapshot.Context;
        ReloadEditorStateFromContext(snapshot.SelectedMaskId, preserveFields: false, markDirty: true);
        selectedMaskLabelText = snapshot.SelectedMaskLabelText;
        selectedMaskAnswerText = snapshot.SelectedMaskAnswerText;
        selectedMaskHintText = snapshot.SelectedMaskHintText;
        selectedTargetOption = TargetOptions.FirstOrDefault(option =>
            string.Equals(option.TargetId, snapshot.SelectedTargetId, StringComparison.Ordinal));
        OnPropertyChanged(nameof(SelectedMaskLabelText));
        OnPropertyChanged(nameof(SelectedMaskAnswerText));
        OnPropertyChanged(nameof(SelectedMaskHintText));
        OnPropertyChanged(nameof(SelectedTargetOption));
        IsDirty = true;
        UpdateCommandStates();
    }

    private void ReloadEditorStateFromContext(
        string? selectMaskId,
        bool preserveFields = false,
        bool markDirty = true)
    {
        ReloadTargetOptions();
        ReloadMaskItems();
        SelectedMaskId = selectMaskId;

        if (!preserveFields)
        {
            SyncSelectedMaskState();
        }

        if (markDirty)
        {
            IsDirty = true;
        }

        UpdateCommandStates();
    }

    private void ReloadTargetOptions()
    {
        Dictionary<string, ImageMaskTargetOptionViewModel> existingByTargetId = TargetOptions
            .ToDictionary(option => option.TargetId, StringComparer.Ordinal);
        ImageMaskTargetOptionViewModel[] nextOptions = currentContext.Slots
            .OrderBy(slot => slot.DisplayClozeIndex)
            .ThenBy(slot => slot.Id, StringComparer.Ordinal)
            .SelectMany(slot => GetTargetsForSlot(slot)
                .Select(target => CreateOrUpdateTargetOption(existingByTargetId, target.Id, slot.Id, slot.DisplayClozeIndex, target.Answer)))
            .ToArray();

        SynchronizeTargetOptions(nextOptions);

        string? selectedTargetId = selectedTargetOption?.TargetId;
        SelectedTargetOption = string.IsNullOrWhiteSpace(selectedTargetId)
            ? null
            : TargetOptions.FirstOrDefault(option => string.Equals(option.TargetId, selectedTargetId, StringComparison.Ordinal));
    }

    private ImageMaskTargetOptionViewModel CreateOrUpdateTargetOption(
        IReadOnlyDictionary<string, ImageMaskTargetOptionViewModel> existingByTargetId,
        string targetId,
        string slotId,
        int displayClozeIndex,
        string answer)
    {
        if (!existingByTargetId.TryGetValue(targetId, out ImageMaskTargetOptionViewModel? existing))
        {
            return new ImageMaskTargetOptionViewModel(targetId, slotId, displayClozeIndex, answer);
        }

        existing.Update(slotId, displayClozeIndex, answer);
        return existing;
    }

    private void SynchronizeTargetOptions(IReadOnlyList<ImageMaskTargetOptionViewModel> nextOptions)
    {
        int sharedCount = Math.Min(TargetOptions.Count, nextOptions.Count);
        for (int index = 0; index < sharedCount; index++)
        {
            if (!ReferenceEquals(TargetOptions[index], nextOptions[index]))
            {
                TargetOptions[index] = nextOptions[index];
            }
        }

        for (int index = TargetOptions.Count - 1; index >= nextOptions.Count; index--)
        {
            TargetOptions.RemoveAt(index);
        }

        for (int index = sharedCount; index < nextOptions.Count; index++)
        {
            TargetOptions.Add(nextOptions[index]);
        }
    }

    private void ReloadMaskItems()
    {
        Dictionary<string, ImageMaskItemViewModel> existingById = Masks.ToDictionary(mask => mask.Id, StringComparer.Ordinal);
        ImageOcclusionMask[] sourceMasks = CurrentImageSurface?.Masks
            .OrderBy(mask => mask.Id, StringComparer.Ordinal)
            .ToArray()
            ?? [];

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ImageOcclusionMask mask in sourceMasks)
        {
            seen.Add(mask.Id);
            if (!existingById.TryGetValue(mask.Id, out ImageMaskItemViewModel? item))
            {
                item = new ImageMaskItemViewModel(mask);
                Masks.Add(item);
            }
            else
            {
                item.Update(mask);
            }

            item.IsInvalid = !IsMaskValid(mask);
            item.VisualRole = ResolveVisualRole(mask, item.IsInvalid, string.Equals(mask.Id, SelectedMaskId, StringComparison.Ordinal));
            item.IsSelected = string.Equals(mask.Id, SelectedMaskId, StringComparison.Ordinal);
        }

        for (int index = Masks.Count - 1; index >= 0; index--)
        {
            if (!seen.Contains(Masks[index].Id))
            {
                Masks.RemoveAt(index);
            }
        }
    }

    private void SyncSelectedMaskState()
    {
        foreach (ImageMaskItemViewModel mask in Masks)
        {
            bool isSelected = string.Equals(mask.Id, SelectedMaskId, StringComparison.Ordinal);
            mask.IsSelected = isSelected;
            mask.VisualRole = ResolveVisualRole(mask.Mask, mask.IsInvalid, isSelected);
        }

        ImageOcclusionMask? selectedMask = FindSelectedMask();
        if (selectedMask is null)
        {
            SelectedTargetOption = null;
            SelectedMaskLabelText = string.Empty;
            SelectedMaskAnswerText = string.Empty;
            SelectedMaskHintText = string.Empty;
            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        CardTarget? linkedTarget = FindTargetById(selectedMask.TargetId);
        SelectedTargetOption = linkedTarget is null
            ? null
            : TargetOptions.FirstOrDefault(option => string.Equals(option.TargetId, linkedTarget.Id, StringComparison.Ordinal));
        SelectedMaskLabelText = selectedMask.Label ?? string.Empty;
        SelectedMaskAnswerText = linkedTarget?.Answer ?? selectedMask.Label ?? string.Empty;
        SelectedMaskHintText = linkedTarget?.Hint ?? string.Empty;
        OnPropertyChanged(nameof(HasSelection));
    }

    private CardContext PersistSelectedMaskFieldsToContext()
    {
        ImageOcclusionMask? selectedMask = FindSelectedMask();
        if (selectedMask is null)
        {
            return currentContext;
        }

        ImageOcclusionMask updatedMask = selectedMask with
        {
            Label = NormalizeOptionalText(SelectedMaskLabelText),
        };

        currentContext = CardContextAuthoring.UpsertMaskOnImageSurface(
            currentContext,
            ImageBlockId,
            ImageRelPath,
            updatedMask);

        if (!string.IsNullOrWhiteSpace(updatedMask.TargetId) && !string.IsNullOrWhiteSpace(SelectedMaskAnswerText))
        {
            currentContext = CardContextAuthoring.UpdateTargetDetails(
                currentContext,
                updatedMask.TargetId,
                SelectedMaskAnswerText,
                SelectedMaskHintText);
        }

        return currentContext;
    }

    private CardContextImageSurface? CurrentImageSurface => currentContext.Images.FirstOrDefault(surface =>
        string.Equals(surface.ImageBlockId, ImageBlockId, StringComparison.Ordinal));

    private IReadOnlyList<CardTarget> GetTargetsForSlot(CardSlot slot)
    {
        return slot.TargetIds.Count == 0
            ? currentContext.Targets.Where(target => string.Equals(target.SlotId, slot.Id, StringComparison.Ordinal)).ToArray()
            : currentContext.Targets.Where(target => slot.TargetIds.Contains(target.Id, StringComparer.Ordinal)).ToArray();
    }

    private CardTarget? FindTargetById(string? targetId)
    {
        return string.IsNullOrWhiteSpace(targetId)
            ? null
            : currentContext.Targets.FirstOrDefault(target => string.Equals(target.Id, targetId, StringComparison.Ordinal));
    }

    private ImageOcclusionMask? FindSelectedMask()
    {
        return FindMaskById(SelectedMaskId);
    }

    private ImageOcclusionMask? FindMaskById(string? maskId)
    {
        return string.IsNullOrWhiteSpace(maskId)
            ? null
            : CurrentImageSurface?.Masks.FirstOrDefault(mask => string.Equals(mask.Id, maskId, StringComparison.Ordinal));
    }

    private string? GetInitialSelectedMaskId()
    {
        return CurrentImageSurface?.Masks.FirstOrDefault()?.Id;
    }

    private static ImageMaskVisualRole ResolveVisualRole(ImageOcclusionMask mask, bool isInvalid, bool isSelected)
    {
        if (mask.IsDisabled)
        {
            return ImageMaskVisualRole.Disabled;
        }

        if (isSelected)
        {
            return ImageMaskVisualRole.Selected;
        }

        if (isInvalid)
        {
            return ImageMaskVisualRole.Invalid;
        }

        return string.IsNullOrWhiteSpace(mask.TargetId)
            ? ImageMaskVisualRole.Draft
            : ImageMaskVisualRole.ResolvedTarget;
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        if (DeleteSelectedMaskCommand is RelayCommand delete)
        {
            delete.RaiseCanExecuteChanged();
        }

        if (LinkSelectedMaskCommand is RelayCommand link)
        {
            link.RaiseCanExecuteChanged();
        }

        if (CreateTargetFromMaskCommand is RelayCommand create)
        {
            create.RaiseCanExecuteChanged();
        }

        if (UnlinkSelectedMaskCommand is RelayCommand unlink)
        {
            unlink.RaiseCanExecuteChanged();
        }

        if (ToggleSelectedMaskDisabledCommand is RelayCommand toggle)
        {
            toggle.RaiseCanExecuteChanged();
        }

        if (UndoCommand is RelayCommand undo)
        {
            undo.RaiseCanExecuteChanged();
        }

        if (RedoCommand is RelayCommand redo)
        {
            redo.RaiseCanExecuteChanged();
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private readonly record struct EditorSnapshot(
        CardContext Context,
        string? SelectedMaskId,
        string SelectedMaskLabelText,
        string SelectedMaskAnswerText,
        string SelectedMaskHintText,
        string? SelectedTargetId);
}
