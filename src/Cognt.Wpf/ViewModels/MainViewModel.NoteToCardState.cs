using System.Collections.ObjectModel;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Features.NoteToCard;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private NoteToCardQueueReadModel? currentNoteToCardQueue;
    private CardCandidateReadModel? selectedCardCandidate;
    private CardPresentationPresetOptionViewModel? selectedNoteToCardPresentationPreset;
    private TextClozeFrontModeOptionViewModel? selectedNoteToCardTextFrontMode;
    private ImageOcclusionFrontModeOptionViewModel? selectedNoteToCardImageFrontMode;
    private CardBackRevealModeOptionViewModel? selectedNoteToCardBackRevealMode;
    private string noteToCardDeckName = string.Empty;
    private IReadOnlyList<string> noteToCardDeckNameCatalog = [];
    private readonly HashSet<string> noteToCardDeckNameSet = new(StringComparer.OrdinalIgnoreCase);
    private string noteToCardPendingTagText = string.Empty;
    private bool isSynchronizingNoteToCardPresentationPreset;

    public ObservableCollection<string> NoteToCardDeckNames { get; } = [];

    public ObservableCollection<string> FilteredNoteToCardDeckNames { get; } = [];

    public ObservableCollection<CardPresentationPresetOptionViewModel> NoteToCardPresentationPresets { get; } = [];

    public ObservableCollection<TextClozeFrontModeOptionViewModel> NoteToCardTextFrontModes { get; } = [];

    public ObservableCollection<ImageOcclusionFrontModeOptionViewModel> NoteToCardImageFrontModes { get; } = [];

    public ObservableCollection<CardBackRevealModeOptionViewModel> NoteToCardBackRevealModes { get; } = [];

    public ObservableCollection<string> NoteToCardTags { get; } = [];

    public NoteToCardQueueReadModel? CurrentNoteToCardQueue
    {
        get => currentNoteToCardQueue;
        private set
        {
            if (SetProperty(ref currentNoteToCardQueue, value))
            {
                OnPropertyChanged(nameof(HasNoteCardCandidates));
                OnPropertyChanged(nameof(NoteCardCandidates));
                OnPropertyChanged(nameof(HasVisibleNoteCardCandidates));
                OnPropertyChanged(nameof(VisibleNoteCardCandidates));
            }
        }
    }

    public bool HasNoteCardCandidates => CurrentNoteToCardQueue?.Items.Count > 0;

    public IReadOnlyList<CardCandidateReadModel> NoteCardCandidates =>
        CurrentNoteToCardQueue?.Items ?? Array.Empty<CardCandidateReadModel>();

    public bool HasVisibleNoteCardCandidates => VisibleNoteCardCandidates.Count > 0;

    public IReadOnlyList<CardCandidateReadModel> VisibleNoteCardCandidates =>
        NoteCardCandidates
            .Where(candidate => candidate.Status != CardCandidateStatus.Disabled)
            .ToArray();

    public bool HasSelectedCardCandidate => SelectedCardCandidate is not null;

    public CardCandidateReadModel? SelectedCardCandidate
    {
        get => selectedCardCandidate;
        set
        {
            if (SetProperty(ref selectedCardCandidate, value))
            {
                OnPropertyChanged(nameof(HasSelectedCardCandidate));
                OnPropertyChanged(nameof(CanDisableSelectedCardCandidate));
                OnPropertyChanged(nameof(CanEditNoteToCardPresentation));
                RefreshNoteToCardPresentationAvailability();
                SynchronizeSelectedNoteToCardPresentationPreset();
            }
        }
    }

    public bool CanDisableSelectedCardCandidate =>
        SelectedCardCandidate is not null
        && SelectedCardCandidate.Status != CardCandidateStatus.Disabled;

    public bool CanEditNoteToCardPresentation => HasVisibleNoteCardCandidates;

    public CardPresentationPresetOptionViewModel? SelectedNoteToCardPresentationPreset
    {
        get => selectedNoteToCardPresentationPreset;
        set
        {
            if (!SetProperty(ref selectedNoteToCardPresentationPreset, value)
                || isSynchronizingNoteToCardPresentationPreset)
            {
                return;
            }

            OnPropertyChanged(nameof(IsCustomNoteToCardPresentationPreset));
            ApplySelectedNoteToCardPresentationPreset(value?.Preset);
        }
    }

    public bool IsCustomNoteToCardPresentationPreset =>
        SelectedNoteToCardPresentationPreset?.Preset == CardPresentationPreset.Custom;

    public TextClozeFrontModeOptionViewModel? SelectedNoteToCardTextFrontMode
    {
        get => selectedNoteToCardTextFrontMode;
        set
        {
            if (!SetProperty(ref selectedNoteToCardTextFrontMode, value)
                || isSynchronizingNoteToCardPresentationPreset)
            {
                return;
            }

            ApplySelectedNoteToCardCustomPresentation();
        }
    }

    public ImageOcclusionFrontModeOptionViewModel? SelectedNoteToCardImageFrontMode
    {
        get => selectedNoteToCardImageFrontMode;
        set
        {
            if (!SetProperty(ref selectedNoteToCardImageFrontMode, value)
                || isSynchronizingNoteToCardPresentationPreset)
            {
                return;
            }

            ApplySelectedNoteToCardCustomPresentation();
        }
    }

    public CardBackRevealModeOptionViewModel? SelectedNoteToCardBackRevealMode
    {
        get => selectedNoteToCardBackRevealMode;
        set
        {
            if (!SetProperty(ref selectedNoteToCardBackRevealMode, value)
                || isSynchronizingNoteToCardPresentationPreset)
            {
                return;
            }

            ApplySelectedNoteToCardCustomPresentation();
        }
    }

    public string NoteToCardDeckName
    {
        get => noteToCardDeckName;
        set
        {
            if (SetProperty(ref noteToCardDeckName, value?.Trim() ?? string.Empty))
            {
                RefreshNoteToCardDeckSearchResults();
            }
        }
    }

    public bool HasFilteredNoteToCardDeckNames => FilteredNoteToCardDeckNames.Count > 0;

    public string NoteToCardPendingTagText
    {
        get => noteToCardPendingTagText;
        set => SetProperty(ref noteToCardPendingTagText, value);
    }
}
