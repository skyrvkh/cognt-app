namespace CogntApp;

public partial class MainWindow
{
    private void BindViewDataContexts()
    {
        DataContext = viewModel;
        settingsView.DataContext = viewModel;
        allNotesView.DataContext = allNotesViewModel;
        noteCardQueueView.DataContext = viewModel;
    }

    private void RegisterViewEvents()
    {
        reviewOverlay.ExitReviewRequested += ReviewView_ExitReviewRequested;
        noteCardQueueView.CreateClozeRequested += NoteCardQueueView_CreateClozeRequested;
        noteCardQueueView.OpenImageOcclusionRequested += NoteCardQueueView_OpenImageOcclusionRequested;

        decksView.PracticeRequested += DecksView_PracticeRequested;
        decksView.CreateCardEditorHostRequested += DecksView_CreateCardEditorHostRequested;
        decksView.CreateCardEditorReleased += DecksView_CreateCardEditorReleased;
        decksView.InfoMessageRequested += DecksView_InfoMessageRequested;
        decksView.WarningMessageRequested += DecksView_WarningMessageRequested;
    }

    private void RegisterViewModelEvents()
    {
        viewModel.OverlayRequested += ViewModel_OverlayRequested;
        viewModel.ClearEditorRequested += ViewModel_ClearEditorRequested;
        viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        viewModel.MentionTargetsChanged += ViewModel_MentionTargetsChanged;
        viewModel.UserMessageRequested += ViewModel_UserMessageRequested;
        viewModel.CardCandidatePreviewRequested += ViewModel_CardCandidatePreviewRequested;
        viewModel.CreateCardContextFromSelectionRequested += ViewModel_CreateCardContextFromSelectionRequested;
        viewModel.AddSelectedTextToSelectedCardSlotRequested += ViewModel_AddSelectedTextToSelectedCardSlotRequested;
        viewModel.CurrentNoteTitleChanged += ViewModel_CurrentNoteTitleChanged;
        viewModel.EditorReadOnlyChanged += ViewModel_EditorReadOnlyChanged;
        viewModel.OpenPdfRequested += ViewModel_OpenPdfRequested;

        allNotesViewModel.OpenNoteRequested += AllNotesViewModel_OpenNoteRequested;
        allNotesViewModel.CreateNoteRequested += AllNotesViewModel_CreateNoteRequested;
        allNotesViewModel.ImportDocumentsRequested += AllNotesViewModel_ImportDocumentsRequested;
    }
}
