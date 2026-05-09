namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class SelectedCardContextBlocksReadModel
{
    public IReadOnlyList<string> BlockIds { get; set; } = [];

    public string? Title { get; set; }
}
