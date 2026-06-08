using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private async void EditorView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ShouldSkipHostRestoreOnLoad())
        {
            UpdateHostStateAfterLoad();
            return;
        }

        await RestoreHostAfterSuspensionAsync();
    }

    private void EditorView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ShouldIgnoreUnloadEvent())
        {
            return;
        }

        FailPendingStartupIfHostNotReady();
        UpdateHostStateAfterUnload();
    }

    private bool ShouldSkipHostRestoreOnLoad()
    {
        return isRestoringHost || !needsHostRestore;
    }

    private void UpdateHostStateAfterLoad()
    {
        hostState = blockSuiteBridge.IsReady ? EditorHostState.Ready : EditorHostState.Loading;
    }

    private bool ShouldIgnoreUnloadEvent()
    {
        return !IsLoaded && hostState == EditorHostState.Unloaded;
    }

    private void FailPendingStartupIfHostNotReady()
    {
        if (!blockSuiteBridge.IsReady)
        {
            blockSuiteBridge.FailPendingStartup(new InvalidOperationException(
                "The editor host was unloaded before BlockSuite finished starting."));
        }
    }

    private void UpdateHostStateAfterUnload()
    {
        hostState = blockSuiteBridge.IsReady ? EditorHostState.Ready : EditorHostState.Unloaded;
    }

    private async Task RestoreHostAfterSuspensionAsync()
    {
        if (isRestoringHost)
        {
            return;
        }

        BeginHostRestoration();

        try
        {
            await WarmUpAsync();
            await RestoreCurrentNoteStateAfterSuspensionAsync();
            CompleteHostRestoration();
        }
        catch (Exception ex)
        {
            HandleHostRestorationFailure(ex);
        }
        finally
        {
            EndHostRestoration();
        }
    }

    private void BeginHostRestoration()
    {
        isRestoringHost = true;
        hostState = EditorHostState.Loading;
        ShowLoadingState("Restoring editor", "Reattaching the note editor...");
    }

    private async Task RestoreCurrentNoteStateAfterSuspensionAsync()
    {
        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            return;
        }

        await ReloadCurrentNoteIntoHostAsync("The editor did not restore the floating note.");
        await ReapplyHostDerivedStateAfterRestorationAsync();
    }

    private async Task ReapplyHostDerivedStateAfterRestorationAsync()
    {
        await FlushMentionTargetsAsync();
        await blockSuiteBridge.SetDocumentLinksAsync(new KnowledgeDocumentLinksReadModel([], []));

        if (currentReadOnlyState)
        {
            await blockSuiteBridge.SetReadOnlyModeAsync(true);
        }
    }

    private void CompleteHostRestoration()
    {
        suppressBridgeCommands = false;
        needsHostRestore = false;
        hostState = EditorHostState.Ready;
        ShowReadyState();
    }

    private void HandleHostRestorationFailure(Exception ex)
    {
        logger.LogError(ex, "EditorView restore failed. Path={Path}", currentSourcePath);
        hostState = EditorHostState.Faulted;
        ShowFailedState(ex.Message, canRetry: true);
    }

    private void EndHostRestoration()
    {
        isRestoringHost = false;
    }
}
