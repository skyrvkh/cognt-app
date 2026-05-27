using CogntApp.ViewModels;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed class ImageMaskTargetOptionViewModel : ObservableObject
{
    private string slotId;
    private int displayClozeIndex;
    private string answer;

    public ImageMaskTargetOptionViewModel(
        string targetId,
        string slotId,
        int displayClozeIndex,
        string answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(answer);

        TargetId = targetId;
        this.slotId = slotId;
        this.displayClozeIndex = displayClozeIndex;
        this.answer = answer;
    }

    public string TargetId { get; }

    public string SlotId
    {
        get => slotId;
        private set
        {
            if (SetProperty(ref slotId, value))
            {
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public int DisplayClozeIndex
    {
        get => displayClozeIndex;
        private set
        {
            if (SetProperty(ref displayClozeIndex, value))
            {
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public string Answer
    {
        get => answer;
        private set
        {
            if (SetProperty(ref answer, value))
            {
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public string DisplayLabel => $"c{DisplayClozeIndex} {Answer}".TrimEnd();

    internal void Update(string slotId, int displayClozeIndex, string answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(answer);

        SlotId = slotId;
        DisplayClozeIndex = displayClozeIndex;
        Answer = answer;
    }
}