using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskEditorViewModel
{
    internal void MarkSaved()
    {
        ReloadEditorStateFromContext(SelectedMaskId, preserveFields: true, markDirty: false);
        IsDirty = false;
        UpdateCommandStates();
    }

    internal void BeginInteractiveChange()
    {
        if (isInteractiveMutationActive)
        {
            return;
        }

        PushUndoSnapshot();
        redoSnapshots.Clear();
        isInteractiveMutationActive = true;
    }

    internal void CompleteInteractiveChange()
    {
        isInteractiveMutationActive = false;
        UpdateCommandStates();
    }

    internal void CancelInteractiveChange()
    {
        if (!isInteractiveMutationActive)
        {
            return;
        }

        isInteractiveMutationActive = false;
        Undo();
    }

    internal bool TrySelectTopmostMaskAtPoint(ImageMaskViewPoint viewPoint, ImageMaskViewRect imageContentRect)
    {
        ImageMaskItemViewModel? hit = Masks
            .OrderByDescending(mask => mask.IsSelected)
            .ThenBy(mask => mask.Id, StringComparer.Ordinal)
            .LastOrDefault(mask => ImageMaskEditorMaskGeometry.HitTest(mask.Mask, viewPoint, imageContentRect));

        SelectedMaskId = hit?.Id;
        return hit is not null;
    }

    internal void SetSelectedMask(string? maskId)
    {
        SelectedMaskId = maskId;
    }

    internal bool CreateMaskFromViewRect(
        ImageMaskShape shape,
        ImageMaskViewRect viewRect,
        ImageMaskViewRect imageContentRect)
    {
        ImageOcclusionMask mask = coordinateMapper.MapViewRectToImageMask(
            ImageMaskEditorMaskGeometry.CreateDraft(shape),
            viewRect,
            imageContentRect);
        if (mask.Width < MinimumNormalizedSize || mask.Height < MinimumNormalizedSize)
        {
            return false;
        }

        RunEditorMutation(() =>
        {
            currentContext = CardContextAuthoring.UpsertMaskOnImageSurface(
                currentContext,
                ImageBlockId,
                ImageRelPath,
                mask);
            ReloadEditorStateFromContext(mask.Id);
        });

        return true;
    }

    internal void UpdateMaskBoundsFromViewRect(
        string maskId,
        ImageMaskViewRect viewRect,
        ImageMaskViewRect imageContentRect)
    {
        ImageOcclusionMask? existingMask = FindMaskById(maskId);
        if (existingMask is null)
        {
            return;
        }

        ImageOcclusionMask mask = coordinateMapper.MapViewRectToImageMask(existingMask, viewRect, imageContentRect);
        currentContext = CardContextAuthoring.UpsertMaskOnImageSurface(
            currentContext,
            ImageBlockId,
            ImageRelPath,
            mask);
        ReloadEditorStateFromContext(maskId, preserveFields: true, markDirty: true);
    }

    internal void MoveSelectedMaskBy(double deltaX, double deltaY)
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null)
        {
            return;
        }

        RunEditorMutation(() =>
        {
            ImageOcclusionMask movedMask = coordinateMapper.ClampToImageBounds(
                ImageMaskEditorMaskGeometry.BuildMoved(existingMask, deltaX, deltaY));
            currentContext = CardContextAuthoring.UpsertMaskOnImageSurface(
                currentContext,
                ImageBlockId,
                ImageRelPath,
                movedMask);
            ReloadEditorStateFromContext(existingMask.Id, preserveFields: true);
        });
    }

    internal void DeleteSelectedMask()
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null)
        {
            return;
        }

        RunEditorMutation(() =>
        {
            currentContext = CardContextAuthoring.DeleteImageMask(currentContext, ImageBlockId, existingMask.Id);
            ReloadEditorStateFromContext(CurrentImageSurface?.Masks.FirstOrDefault()?.Id);
        });
    }

    internal void UnlinkSelectedMask()
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null)
        {
            return;
        }

        RunEditorMutation(() =>
        {
            currentContext = CardContextAuthoring.UnlinkImageMask(currentContext, ImageBlockId, existingMask.Id);
            ReloadEditorStateFromContext(existingMask.Id);
        });
    }

    internal void ToggleSelectedMaskDisabled()
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null)
        {
            return;
        }

        RunEditorMutation(() =>
        {
            currentContext = CardContextAuthoring.SetImageMaskDisabled(
                currentContext,
                ImageBlockId,
                existingMask.Id,
                !existingMask.IsDisabled);
            ReloadEditorStateFromContext(existingMask.Id);
        });
    }

    internal void AttachSelectedMaskToSelectedTarget()
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null || SelectedTargetOption is null)
        {
            return;
        }

        RunEditorMutation(() =>
        {
            currentContext = PersistSelectedMaskFieldsToContext();
            currentContext = CardContextAuthoring.AttachImageMaskToTarget(
                currentContext,
                SelectedTargetOption.TargetId,
                CardContextAuthoring.CreateImageMaskAnchor(ImageBlockId, existingMask.Id),
                existingMask with
                {
                    Label = NormalizeOptionalText(SelectedMaskLabelText),
                },
                ImageRelPath);
            currentContext = CardContextAuthoring.UpdateTargetDetails(
                currentContext,
                SelectedTargetOption.TargetId,
                SelectedMaskAnswerText,
                SelectedMaskHintText);
            ReloadEditorStateFromContext(existingMask.Id);
        });
    }

    internal void CreateTargetForSelectedMask()
    {
        ImageOcclusionMask? existingMask = FindSelectedMask();
        if (existingMask is null || string.IsNullOrWhiteSpace(SelectedMaskAnswerText))
        {
            return;
        }

        RunEditorMutation(() =>
        {
            currentContext = PersistSelectedMaskFieldsToContext();
            currentContext = CardContextAuthoring.AddImageMaskTargetToNewSlot(
                currentContext,
                ImageBlockId,
                ImageRelPath,
                existingMask with
                {
                    Label = NormalizeOptionalText(SelectedMaskLabelText) ?? NormalizeOptionalText(SelectedMaskAnswerText),
                },
                SelectedMaskAnswerText,
                SelectedMaskHintText);
            ReloadEditorStateFromContext(existingMask.Id);
        });
    }

    internal void Undo()
    {
        if (undoSnapshots.Count == 0)
        {
            return;
        }

        redoSnapshots.Push(CreateSnapshot());
        RestoreSnapshot(undoSnapshots.Pop());
    }

    internal void Redo()
    {
        if (redoSnapshots.Count == 0)
        {
            return;
        }

        undoSnapshots.Push(CreateSnapshot());
        RestoreSnapshot(redoSnapshots.Pop());
    }

}
