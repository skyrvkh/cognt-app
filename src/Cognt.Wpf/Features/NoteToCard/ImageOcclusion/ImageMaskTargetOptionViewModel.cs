namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed record ImageMaskTargetOptionViewModel(
    string TargetId,
    string SlotId,
    int DisplayClozeIndex,
    string Answer)
{
    public string DisplayLabel => $"c{DisplayClozeIndex} {Answer}".TrimEnd();
}
