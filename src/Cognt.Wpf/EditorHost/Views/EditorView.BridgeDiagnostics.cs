using Cognt.Wpf.EditorHost.Services;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private void BlockSuiteBridge_DiagnosticReceived(object? sender, EditorDiagnosticReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Message))
        {
            return;
        }

        switch (e.Level)
        {
            case EditorDiagnosticLevel.Error:
                logger.LogError("BlockSuite editor diagnostic: {DiagnosticMessage}", e.Message);
                break;
            case EditorDiagnosticLevel.Warning:
                logger.LogWarning("BlockSuite editor diagnostic: {DiagnosticMessage}", e.Message);
                break;
            default:
                logger.LogDebug("BlockSuite editor diagnostic: {DiagnosticMessage}", e.Message);
                break;
        }
    }
}
