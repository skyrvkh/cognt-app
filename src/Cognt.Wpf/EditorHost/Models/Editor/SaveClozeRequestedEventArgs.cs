namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class SaveClozeRequestedEventArgs(
    string sourcePath,
    string blockId,
    string selectedPlainText,
    string clozeText,
    string content,
    string? cardContextId = null,
    string? slotId = null,
    int? displayClozeIndex = null) : EventArgs
{
    public string SourcePath { get; } = string.IsNullOrWhiteSpace(sourcePath)
        ? throw new ArgumentException("Source path is required.", nameof(sourcePath))
        : sourcePath;

    public string BlockId { get; } = string.IsNullOrWhiteSpace(blockId)
        ? throw new ArgumentException("Block ID is required.", nameof(blockId))
        : blockId;

    public string SelectedPlainText { get; } = string.IsNullOrWhiteSpace(selectedPlainText)
        ? throw new ArgumentException("The selected text is required.", nameof(selectedPlainText))
        : selectedPlainText;

    public string ClozeText { get; } = string.IsNullOrWhiteSpace(clozeText)
        ? throw new ArgumentException("The cloze text is required.", nameof(clozeText))
        : clozeText;

    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public string? CardContextId { get; } = string.IsNullOrWhiteSpace(cardContextId)
        ? null
        : cardContextId.Trim();

    public string? SlotId { get; } = string.IsNullOrWhiteSpace(slotId)
        ? null
        : slotId.Trim();

    public int? DisplayClozeIndex { get; } = displayClozeIndex is > 0
        ? displayClozeIndex.Value
        : null;
}
