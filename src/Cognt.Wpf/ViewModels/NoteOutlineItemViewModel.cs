using System.Windows;

namespace CogntApp.ViewModels;

public sealed class NoteOutlineItemViewModel
{
    public NoteOutlineItemViewModel(string title, int level, string blockId)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Untitled heading" : title.Trim();
        Level = Math.Clamp(level, 1, 6);
        BlockId = blockId;
    }

    public string Title { get; }

    public int Level { get; }

    public string BlockId { get; }

    public Thickness Indent => new((Level - 1) * 8.0d, 0, 0, 0);
}
