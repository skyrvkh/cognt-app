namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class EditorCaretState
{
    public string BlockId { get; set; } = string.Empty;

    public int Index { get; set; }

    public int Length { get; set; }

    public bool IsValid => !string.IsNullOrWhiteSpace(BlockId)
        && Index >= 0
        && Length >= 0;
}
