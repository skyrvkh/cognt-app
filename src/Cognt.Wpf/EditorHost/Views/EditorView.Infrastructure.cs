using Cognt.Wpf.EditorHost.Components;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView
{
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
        blockSuiteBridge.DocumentChanged += BlockSuiteBridge_DocumentChanged;
        blockSuiteBridge.ContentChanged += BlockSuiteBridge_ContentChanged;
        blockSuiteBridge.CreateExtractRequested += BlockSuiteBridge_CreateExtractRequested;
        blockSuiteBridge.SaveClozeRequested += BlockSuiteBridge_SaveClozeRequested;
        blockSuiteBridge.OpenImageOcclusionRequested += BlockSuiteBridge_OpenImageOcclusionRequested;
        blockSuiteBridge.MentionSearchRequested += BlockSuiteBridge_MentionSearchRequested;
        blockSuiteBridge.MentionPreviewRequested += BlockSuiteBridge_MentionPreviewRequested;
        blockSuiteBridge.OpenMentionRequested += BlockSuiteBridge_OpenMentionRequested;
        blockSuiteBridge.CreateLinkedNoteRequested += BlockSuiteBridge_CreateLinkedNoteRequested;
        blockSuiteBridge.AttachFileRequested += BlockSuiteBridge_AttachFileRequested;
        blockSuiteBridge.AttachImageSourceRequested += BlockSuiteBridge_AttachImageSourceRequested;
        blockSuiteBridge.ReadAttachmentRequested += BlockSuiteBridge_ReadAttachmentRequested;
        blockSuiteBridge.StoreEditorBlobRequested += BlockSuiteBridge_StoreEditorBlobRequested;
        blockSuiteBridge.ReadEditorBlobRequested += BlockSuiteBridge_ReadEditorBlobRequested;
        blockSuiteBridge.ListEditorBlobsRequested += BlockSuiteBridge_ListEditorBlobsRequested;
        blockSuiteBridge.DiagnosticReceived += BlockSuiteBridge_DiagnosticReceived;
    }
}
