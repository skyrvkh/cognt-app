using Cognt.Wpf.EditorHost.Components;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
    private static readonly TimeSpan EditorOperationTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan HostStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinimumDocumentLoadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumDocumentLoadTimeout = TimeSpan.FromMinutes(4);

    private void ConfigureBlockSuiteHost()
    {
        BlockSuiteHostView.Services = serviceProvider;
        BlockSuiteHostView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(BlockSuiteHost),
            Parameters = new Dictionary<string, object?>
            {
                [nameof(BlockSuiteHost.Bridge)] = blockSuiteBridge,
            },
        });
    }

    private void BindBlockSuiteBridge()
    {
        blockSuiteBridge.ContentChanged += BlockSuiteBridge_ContentChanged;
        blockSuiteBridge.CreateExtractRequested += BlockSuiteBridge_CreateExtractRequested;
        blockSuiteBridge.SaveClozeRequested += BlockSuiteBridge_SaveClozeRequested;
        blockSuiteBridge.OpenImageOcclusionRequested += BlockSuiteBridge_OpenImageOcclusionRequested;
        blockSuiteBridge.MentionSearchRequested += BlockSuiteBridge_MentionSearchRequested;
        blockSuiteBridge.MentionPreviewRequested += BlockSuiteBridge_MentionPreviewRequested;
        blockSuiteBridge.OpenMentionRequested += BlockSuiteBridge_OpenMentionRequested;
        blockSuiteBridge.CreateLinkedNoteRequested += BlockSuiteBridge_CreateLinkedNoteRequested;
        blockSuiteBridge.AttachFileRequested += BlockSuiteBridge_AttachFileRequested;
        blockSuiteBridge.ReadAttachmentRequested += BlockSuiteBridge_ReadAttachmentRequested;
        blockSuiteBridge.DiagnosticReceived += BlockSuiteBridge_DiagnosticReceived;
    }

    private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask != task)
        {
            RunBlockSuiteCommand(task, "timed-out operation");
            throw new TimeoutException(timeoutMessage);
        }

        await task;
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask != task)
        {
            RunBlockSuiteCommand(task, "timed-out operation");
            throw new TimeoutException(timeoutMessage);
        }

        return await task;
    }

    private static TimeSpan GetDocumentLoadTimeout(string? content)
    {
        int contentLength = content?.Length ?? 0;
        int totalSeconds = 30 + (int)Math.Ceiling(contentLength / 10_000d);

        return TimeSpan.FromSeconds(Math.Clamp(
            totalSeconds,
            (int)MinimumDocumentLoadTimeout.TotalSeconds,
            (int)MaximumDocumentLoadTimeout.TotalSeconds));
    }

    private static void RunBlockSuiteCommand(Task task, string operation)
    {
        _ = ObserveBlockSuiteCommandAsync(task, operation);
    }

    private static async Task ObserveBlockSuiteCommandAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite {operation} failed: {ex}");
        }
    }
}
