using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Features.NoteToCard;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public ICommand LoadCollectionCommand { get; private set; } = null!;

    public ICommand ShowDashboardCommand { get; private set; } = null!;

    public ICommand ShowAllNotesCommand { get; private set; } = null!;

    public ICommand ShowGraphCommand { get; private set; } = null!;

    public ICommand ShowDecksCommand { get; private set; } = null!;

    public ICommand ShowSettingsCommand { get; private set; } = null!;

    public ICommand SelectCollectionCommand { get; private set; } = null!;

    public ICommand AddCollectionCommand { get; private set; } = null!;

    public ICommand RemoveCollectionCommand { get; private set; } = null!;

    public ICommand AddNoteCommand { get; private set; } = null!;

    public ICommand OpenNoteCommand { get; private set; } = null!;

    public ICommand OpenGlobalSearchResultCommand { get; private set; } = null!;

    public ICommand RetryGlobalSearchCommand { get; private set; } = null!;

    public ICommand SaveReviewSettingsCommand { get; private set; } = null!;

    public ICommand ResetReviewSettingsCommand { get; private set; } = null!;

    public ICommand SaveGraphSettingsCommand { get; private set; } = null!;

    public ICommand ResetGraphSettingsCommand { get; private set; } = null!;

    public ICommand SaveEditorSettingsCommand { get; private set; } = null!;

    public ICommand ResetEditorSettingsCommand { get; private set; } = null!;

    public ICommand GenerateReadyNoteCardsCommand { get; private set; } = null!;

    public ICommand GenerateSelectedNoteCardCommand { get; private set; } = null!;

    public ICommand PreviewSelectedCardCandidateCommand { get; private set; } = null!;

    public ICommand CreateCardContextFromSelectionCommand { get; private set; } = null!;

    public ICommand AddSelectedTextToSelectedCardSlotCommand { get; private set; } = null!;

    public ICommand DisableSelectedCardCandidateCommand { get; private set; } = null!;

    public ICommand AddNoteToCardTagCommand { get; private set; } = null!;

    public ICommand RemoveNoteToCardTagCommand { get; private set; } = null!;

    public ICommand SelectNoteToCardDeckCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        LoadCollectionCommand = CreateUserActionCommand(
            "Load Collection Failed",
            () => LoadCollection(CollectionPath));
        ShowDashboardCommand = CreateUserActionCommand("Navigation Failed", ShowDashboard);
        ShowAllNotesCommand = CreateUserActionCommand("Navigation Failed", ShowAllNotes);
        ShowGraphCommand = CreateUserActionCommand("Navigation Failed", ShowGraph);
        ShowDecksCommand = CreateUserActionCommand("Navigation Failed", ShowDecks);
        ShowSettingsCommand = CreateUserActionCommand("Navigation Failed", ShowSettings);
        SelectCollectionCommand = CreateUserActionCommand("Switch Collection Failed", SelectCollection);
        AddCollectionCommand = CreateUserActionCommand("Create Collection Failed", AddCollection);
        RemoveCollectionCommand = CreateUserActionCommand("Remove Collection Failed", RemoveCurrentCollection);
        AddNoteCommand = CreateUserActionCommand("Create Note Failed", AddNote);
        OpenNoteCommand = CreateUserActionCommand("Open Note Failed", OpenNoteFromCommand);
        OpenGlobalSearchResultCommand = CreateUserActionCommand("Open Search Result Failed", OpenGlobalSearchResult);
        RetryGlobalSearchCommand = new RelayCommand(() => QueueGlobalSearch(immediate: true));
        SaveReviewSettingsCommand = new RelayCommand(SaveReviewSettings);
        ResetReviewSettingsCommand = new RelayCommand(ResetReviewSettings);
        SaveGraphSettingsCommand = new RelayCommand(SaveGraphSettings);
        ResetGraphSettingsCommand = new RelayCommand(ResetGraphSettings);
        SaveEditorSettingsCommand = new RelayCommand(_ => SaveEditorSettings(), _ => CanSaveEditorSettings);
        ResetEditorSettingsCommand = new RelayCommand(ResetEditorSettings);
        GenerateReadyNoteCardsCommand = new RelayCommand(() => _ = GenerateReadyCardCandidatesAsync());
        GenerateSelectedNoteCardCommand = new RelayCommand(() => _ = GenerateSelectedCardCandidateAsync());
        PreviewSelectedCardCandidateCommand = new RelayCommand(PreviewSelectedCardCandidate);
        CreateCardContextFromSelectionCommand = new RelayCommand(RaiseCreateCardContextFromSelectionRequest);
        AddSelectedTextToSelectedCardSlotCommand = new RelayCommand(RaiseAddSelectedTextToSelectedCardSlotRequest);
        DisableSelectedCardCandidateCommand = new RelayCommand(DisableCurrentlySelectedCardCandidate);
        AddNoteToCardTagCommand = new RelayCommand(CommitPendingNoteToCardTags);
        RemoveNoteToCardTagCommand = new RelayCommand(RemoveNoteToCardTag);
        SelectNoteToCardDeckCommand = new RelayCommand(SelectNoteToCardDeck);
    }

    private void InitializeNoteToCardPresentationOptions()
    {
        foreach (CardPresentationPreset preset in CardPresentationPolicies.UserSelectablePresets)
        {
            NoteToCardPresentationPresets.Add(new CardPresentationPresetOptionViewModel(
                preset,
                CardPresentationPolicies.GetDisplayLabel(preset)));
        }

        RefreshNoteToCardPresentationAvailability();

        foreach (TextClozeFrontMode mode in Enum.GetValues<TextClozeFrontMode>())
        {
            NoteToCardTextFrontModes.Add(new TextClozeFrontModeOptionViewModel(
                mode,
                FormatTextFrontMode(mode)));
        }

        foreach (ImageOcclusionFrontMode mode in Enum.GetValues<ImageOcclusionFrontMode>().Distinct())
        {
            NoteToCardImageFrontModes.Add(new ImageOcclusionFrontModeOptionViewModel(
                mode,
                FormatImageFrontMode(mode)));
        }

        foreach (CardBackRevealMode mode in Enum.GetValues<CardBackRevealMode>())
        {
            NoteToCardBackRevealModes.Add(new CardBackRevealModeOptionViewModel(
                mode,
                FormatBackRevealMode(mode)));
        }
    }

    private RelayCommand CreateUserActionCommand(string errorTitle, Action action)
    {
        return new RelayCommand(() => RunUserAction(errorTitle, action));
    }

    private RelayCommand CreateUserActionCommand(string errorTitle, Action<object?> action)
    {
        return new RelayCommand(parameter => RunUserAction(errorTitle, () => action(parameter)));
    }
}
