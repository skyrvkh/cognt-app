using Cognt.Wpf.EditorHost.Models.Editor;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public Task<SelectedImageBlockReadModel?> GetSelectedImageBlockAsync()
    {
        return blockSuiteBridge.GetSelectedImageBlockAsync();
    }

    public Task<SelectedCardContextBlocksReadModel?> GetSelectedCardContextBlocksAsync()
    {
        return blockSuiteBridge.GetSelectedCardContextBlocksAsync();
    }

    public Task CreateClozeFromSelectionAsync(
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        return blockSuiteBridge.CreateClozeFromSelectionAsync(cardContextId, slotId, displayClozeIndex);
    }

    public async Task FocusBlockAsync(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        if (!IsHostReady)
        {
            await WarmUpAsync();
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.FocusBlockAsync(blockId),
            EditorOperationTimeout,
            "The editor did not navigate to the selected heading.");
    }

    public Task<EditorCaretState?> GetCaretStateAsync()
    {
        return !IsHostReady || string.IsNullOrWhiteSpace(currentSourcePath)
            ? Task.FromResult<EditorCaretState?>(null)
            : blockSuiteBridge.GetCaretStateAsync();
    }

    public async Task RestoreCaretStateAsync(EditorCaretState caret)
    {
        ArgumentNullException.ThrowIfNull(caret);

        if (!caret.IsValid)
        {
            return;
        }

        if (!IsHostReady)
        {
            await WarmUpAsync();
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.RestoreCaretStateAsync(caret),
            EditorOperationTimeout,
            "The editor did not restore the caret.");
    }
}
