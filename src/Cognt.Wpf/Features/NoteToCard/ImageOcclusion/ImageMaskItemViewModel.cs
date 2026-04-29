using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.ViewModels;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed class ImageMaskItemViewModel(ImageOcclusionMask mask) : ObservableObject
{
    private ImageOcclusionMask currentMask = mask ?? throw new ArgumentNullException(nameof(mask));
    private bool isSelected;
    private bool isInvalid;
    private ImageMaskVisualRole visualRole = ImageMaskVisualRole.Draft;

    public string Id => currentMask.Id;

    public string TargetId => currentMask.TargetId;

    public ImageMaskShape Shape => currentMask.Shape;

    public string? Label => currentMask.Label;

    public bool IsDisabled => currentMask.IsDisabled;

    public ImageOcclusionMask Mask
    {
        get => currentMask;
        private set
        {
            currentMask = value;
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(TargetId));
            OnPropertyChanged(nameof(Shape));
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(IsDisabled));
            OnPropertyChanged(nameof(Mask));
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsInvalid
    {
        get => isInvalid;
        set => SetProperty(ref isInvalid, value);
    }

    public ImageMaskVisualRole VisualRole
    {
        get => visualRole;
        set => SetProperty(ref visualRole, value);
    }

    internal void Update(ImageOcclusionMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Mask = mask;
    }
}
