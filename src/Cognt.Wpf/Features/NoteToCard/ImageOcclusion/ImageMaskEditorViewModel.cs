using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.ViewModels;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

public sealed partial class ImageMaskEditorViewModel : ObservableObject
{
    private const double MinimumNormalizedSize = 0.01d;

    private readonly Stack<EditorSnapshot> undoSnapshots = [];
    private readonly Stack<EditorSnapshot> redoSnapshots = [];
    private readonly ImageMaskCoordinateMapper coordinateMapper = new();
    private CardContext currentContext;
    private string? selectedMaskId;
    private ImageMaskTargetOptionViewModel? selectedTargetOption;
    private string selectedMaskLabelText = string.Empty;
    private string selectedMaskAnswerText = string.Empty;
    private string selectedMaskHintText = string.Empty;
    private ImageMaskEditorTool currentTool = ImageMaskEditorTool.Select;
    private string? previewSlotId;
    private bool isDirty;
    private bool isInteractiveMutationActive;

    public ImageMaskEditorViewModel(OpenImageMaskEditorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        currentContext = request.Context;
        ImageSource = LoadImageSource(request.ImageFullPath);
        Masks = [];
        TargetOptions = [];

        SelectToolCommand = new RelayCommand(parameter =>
        {
            if (parameter is ImageMaskEditorTool tool)
            {
                CurrentTool = tool;
                return;
            }

            if (parameter is string text && Enum.TryParse<ImageMaskEditorTool>(text, ignoreCase: true, out ImageMaskEditorTool parsed))
            {
                CurrentTool = parsed;
            }
        });
        DeleteSelectedMaskCommand = new RelayCommand(_ => DeleteSelectedMask(), _ => HasSelection);
        LinkSelectedMaskCommand = new RelayCommand(
            _ => AttachSelectedMaskToSelectedTarget(),
            _ => HasSelection && SelectedTargetOption is not null);
        CreateTargetFromMaskCommand = new RelayCommand(
            _ => CreateTargetForSelectedMask(),
            _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedMaskAnswerText));
        UnlinkSelectedMaskCommand = new RelayCommand(_ => UnlinkSelectedMask(), _ => HasSelection);
        ToggleSelectedMaskDisabledCommand = new RelayCommand(_ => ToggleSelectedMaskDisabled(), _ => HasSelection);
        UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);
        RedoCommand = new RelayCommand(_ => Redo(), _ => CanRedo);

        ReloadEditorStateFromContext(selectMaskId: GetInitialSelectedMaskId(), markDirty: false);
    }

    public OpenImageMaskEditorRequest Request { get; }

    public ObservableCollection<ImageMaskItemViewModel> Masks { get; }

    public ObservableCollection<ImageMaskTargetOptionViewModel> TargetOptions { get; }

    public BitmapImage ImageSource { get; }

    public ICommand SelectToolCommand { get; }

    public ICommand DeleteSelectedMaskCommand { get; }

    public ICommand LinkSelectedMaskCommand { get; }

    public ICommand CreateTargetFromMaskCommand { get; }

    public ICommand UnlinkSelectedMaskCommand { get; }

    public ICommand ToggleSelectedMaskDisabledCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public string SourceRelPath => Request.SourceRelPath;

    public string ImageBlockId => Request.ImageBlockId;

    public string ImageRelPath => Request.ImageRelPath;

    public string ContextId => Request.ContextId ?? currentContext.Id;

    public CardContext Context => currentContext;

    public CardPresentationPolicy Presentation => CardPresentationPolicies.Resolve(currentContext);

    public bool IsDirty
    {
        get => isDirty;
        private set => SetProperty(ref isDirty, value);
    }

    public ImageMaskEditorTool CurrentTool
    {
        get => currentTool;
        set => SetProperty(ref currentTool, value);
    }

    public string? SelectedMaskId
    {
        get => selectedMaskId;
        set
        {
            if (SetProperty(ref selectedMaskId, value))
            {
                SyncSelectedMaskState();
                UpdateCommandStates();
            }
        }
    }

    public ImageMaskTargetOptionViewModel? SelectedTargetOption
    {
        get => selectedTargetOption;
        set
        {
            if (SetProperty(ref selectedTargetOption, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string SelectedMaskLabelText
    {
        get => selectedMaskLabelText;
        set => SetProperty(ref selectedMaskLabelText, value);
    }

    public string SelectedMaskAnswerText
    {
        get => selectedMaskAnswerText;
        set
        {
            if (SetProperty(ref selectedMaskAnswerText, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string SelectedMaskHintText
    {
        get => selectedMaskHintText;
        set => SetProperty(ref selectedMaskHintText, value);
    }

    public bool HasSelection => SelectedMask is not null;

    public ImageMaskItemViewModel? SelectedMask => Masks.FirstOrDefault(mask =>
        string.Equals(mask.Id, SelectedMaskId, StringComparison.Ordinal));

    public bool CanUndo => undoSnapshots.Count > 0;

    public bool CanRedo => redoSnapshots.Count > 0;

    public ImageMaskCoordinateMapper CoordinateMapper => coordinateMapper;

    public string? PreviewSlotId
    {
        get => previewSlotId;
        set => SetProperty(ref previewSlotId, value);
    }

    internal ImageMaskEditorResult BuildResult(bool saved)
    {
        PersistSelectedMaskFieldsToContext();

        return new ImageMaskEditorResult(
            saved,
            SourceRelPath,
            ImageBlockId,
            ContextId,
            currentContext,
            CurrentImageSurface?.Masks ?? []);
    }

    private static BitmapImage LoadImageSource(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The image file could not be found.", fullPath);
        }

        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
