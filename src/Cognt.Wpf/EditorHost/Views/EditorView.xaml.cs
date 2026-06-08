using System.Windows.Controls;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.Extensions.Logging;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView : UserControl
{
    private enum EditorHostState
    {
        Unloaded,
        Loading,
        Ready,
        Suspended,
        Faulted,
    }

    private readonly BlockSuiteHostBridge blockSuiteBridge;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<EditorView> logger;
    private IReadOnlyList<KnowledgeMentionTargetReadModel> mentionTargets = [];
    private string currentContent = string.Empty;
    private string currentTitle = string.Empty;
    private string? currentSourcePath;
    private string? currentCollectionRoot;
    private Action<string>? transientContentChanged;
    private int loadRequestId;
    private bool isShowingTransientEditor;
    private EditorHostState hostState = EditorHostState.Unloaded;
    private bool needsHostRestore;
    private bool isRestoringHost;
    private bool suppressBridgeCommands;
    private bool currentReadOnlyState;
    private EditorTheme? currentTheme;
    private bool isDisposingEditorHost;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler? DocumentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    public event EventHandler<OpenImageOcclusionRequestedEventArgs>? OpenImageOcclusionRequested;
    public event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    public event Func<object?, AttachFileRequestedEventArgs, Task>? AttachFileRequested;
    public event Func<object?, AttachImageSourceRequestedEventArgs, Task>? AttachImageSourceRequested;
    public event Func<object?, ReadAttachmentRequestedEventArgs, Task>? ReadAttachmentRequested;
    public event Func<object?, StoreEditorBlobRequestedEventArgs, Task>? StoreEditorBlobRequested;
    public event Func<object?, ReadEditorBlobRequestedEventArgs, Task>? ReadEditorBlobRequested;
    public event Func<object?, ListEditorBlobsRequestedEventArgs, Task>? ListEditorBlobsRequested;
    public event EventHandler? RetryRequested;

    public bool IsShowingTransientEditor => isShowingTransientEditor;

    public EditorView(
        IServiceProvider serviceProvider,
        ILogger<EditorView> logger,
        ILogger<BlockSuiteHostBridge> bridgeLogger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        blockSuiteBridge = new BlockSuiteHostBridge(bridgeLogger);
        InitializeComponent();
        ConfigureBlockSuiteHost();
        BindBlockSuiteBridge();
        Loaded += EditorView_Loaded;
        Unloaded += EditorView_Unloaded;
        ShowNoNoteState();
    }
}
