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
            .LastOrDefault(mask => HitTestMask(mask.Mask, viewPoint, imageContentRect));

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
            CreateDraftMask(shape),
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
                BuildMovedMask(existingMask, deltaX, deltaY));
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

    private static ImageOcclusionMask CreateDraftMask(ImageMaskShape shape)
    {
        return shape switch
        {
            ImageMaskShape.Ellipse => CardContextAuthoring.CreateEllipseMask(
                $"mask_{Guid.NewGuid():N}",
                targetId: null,
                cx: 0.5d,
                cy: 0.5d,
                rx: 0.1d,
                ry: 0.1d),
            _ => CardContextAuthoring.CreateRectangleMask(
                $"mask_{Guid.NewGuid():N}",
                targetId: null,
                x: 0d,
                y: 0d,
                width: 0d,
                height: 0d),
        };
    }

    private static ImageOcclusionMask BuildMovedMask(ImageOcclusionMask existingMask, double deltaX, double deltaY)
    {
        return existingMask.Shape switch
        {
            ImageMaskShape.Ellipse => existingMask with
            {
                Cx = (existingMask.Cx ?? (existingMask.X + (existingMask.Width / 2d))) + deltaX,
                Cy = (existingMask.Cy ?? (existingMask.Y + (existingMask.Height / 2d))) + deltaY,
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
            },
            ImageMaskShape.Polygon => existingMask with
            {
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
                Points = existingMask.Points?.Select(point => new NormalizedPoint(
                    point.X + deltaX,
                    point.Y + deltaY)).ToArray(),
            },
            _ => existingMask with
            {
                X = existingMask.X + deltaX,
                Y = existingMask.Y + deltaY,
            },
        };
    }

    private static bool HitTestMask(
        ImageOcclusionMask mask,
        ImageMaskViewPoint point,
        ImageMaskViewRect imageContentRect)
    {
        ImageMaskCoordinateMapper mapper = new();
        ImageMaskViewRect rect = mapper.MapNormalizedMaskToViewRect(mask, imageContentRect);
        if (rect.IsEmpty)
        {
            return false;
        }

        if (mask.Shape == ImageMaskShape.Ellipse)
        {
            double radiusX = rect.Width / 2d;
            double radiusY = rect.Height / 2d;
            if (radiusX <= 0d || radiusY <= 0d)
            {
                return false;
            }

            double centerX = rect.X + radiusX;
            double centerY = rect.Y + radiusY;
            double dx = (point.X - centerX) / radiusX;
            double dy = (point.Y - centerY) / radiusY;
            return (dx * dx) + (dy * dy) <= 1d;
        }

        return point.X >= rect.X
            && point.X <= rect.Right
            && point.Y >= rect.Y
            && point.Y <= rect.Bottom;
    }
}
