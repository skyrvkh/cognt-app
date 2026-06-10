namespace CogntApp;

public partial class MainWindow
{
    private void BindViewDataContexts()
    {
        DataContext = viewModel;
        dashboardView.DataContext = viewModel;
        settingsView.DataContext = viewModel;
        allNotesView.DataContext = allNotesViewModel;
        graphView.DataContext = graphViewModel;
        noteInspectorView.DataContext = noteInspectorViewModel;
        noteGraphToolView.DataContext = graphViewModel;
        noteCardQueueView.DataContext = viewModel;
    }

    private void RegisterViewEvents()
    {
        reviewOverlay.ExitReviewRequested += ReviewView_ExitReviewRequested;
        noteCardQueueView.CreateClozeRequested += NoteCardQueueView_CreateClozeRequested;
        noteCardQueueView.OpenImageOcclusionRequested += NoteCardQueueView_OpenImageOcclusionRequested;

        decksView.PracticeRequested += DecksView_PracticeRequested;
        decksView.CramRequested += DecksView_CramRequested;
        decksView.InfoMessageRequested += DecksView_InfoMessageRequested;
        decksView.WarningMessageRequested += DecksView_WarningMessageRequested;
    }

    private void RegisterViewModelEvents()
    {
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.OverlayRequested += ViewModel_OverlayRequested;
        viewModel.ClearEditorRequested += ViewModel_ClearEditorRequested;
        viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        viewModel.NoteDocumentsDeleted += ViewModel_NoteDocumentsDeleted;
        viewModel.MentionTargetsChanged += ViewModel_MentionTargetsChanged;
        viewModel.UserMessageRequested += ViewModel_UserMessageRequested;
        viewModel.CardCandidatePreviewRequested += ViewModel_CardCandidatePreviewRequested;
        viewModel.AddSelectedTextToSelectedCardSlotRequested += ViewModel_AddSelectedTextToSelectedCardSlotRequested;
        viewModel.CurrentNoteTitleChanged += ViewModel_CurrentNoteTitleChanged;
        viewModel.CurrentNoteContentChanged += ViewModel_CurrentNoteContentChanged;
        viewModel.EditorReadOnlyChanged += ViewModel_EditorReadOnlyChanged;
        viewModel.OpenPdfRequested += ViewModel_OpenPdfRequested;
        noteInspectorViewModel.OutlineNavigationRequested += NoteInspectorViewModel_OutlineNavigationRequested;

        allNotesViewModel.OpenNoteRequested += AllNotesViewModel_OpenNoteRequested;
        allNotesViewModel.ShowCardsRequested += AllNotesViewModel_ShowCardsRequested;
        allNotesViewModel.TagEditRequested += AllNotesViewModel_TagEditRequested;
        allNotesViewModel.CreateNoteRequested += AllNotesViewModel_CreateNoteRequested;
        allNotesViewModel.ImportDocumentsRequested += AllNotesViewModel_ImportDocumentsRequested;
    }
}
