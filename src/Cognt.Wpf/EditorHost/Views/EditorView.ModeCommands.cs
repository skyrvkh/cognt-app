using Cognt.Wpf.EditorHost.Models.Editor;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    public async Task SetReadOnlyAsync(bool isReadOnly)
    {
        currentReadOnlyState = isReadOnly;
        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            return;
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.SetReadOnlyModeAsync(isReadOnly),
            EditorOperationTimeout,
            "The editor did not change modes.");
    }

    public void ApplyTheme(EditorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        currentTheme = theme;
        RunBlockSuiteCommand(
            blockSuiteBridge.ApplyThemeAsync(theme),
            "theme update");
    }
}
