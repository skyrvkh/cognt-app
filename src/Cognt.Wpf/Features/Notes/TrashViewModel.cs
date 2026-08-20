using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using CogntApp.ViewModels;

namespace CogntApp.Features.Notes;

/// <summary>
/// Coarse-grained state of the trash view-model.
/// </summary>
public enum TrashViewState
{
    /// <summary>No trash is currently being processed.</summary>
    Idle = 0,
    /// <summary>Trash list is loading.</summary>
    Loading = 1,
    /// <summary>Trash list is ready for the user.</summary>
    Ready = 2,
    /// <summary>An Empty Trash or restore operation is in flight.</summary>
    Deleting = 3,
    /// <summary>Empty Trash or restore failed; the trash list is preserved.</summary>
    Failed = 4,
}

/// <summary>
/// One row in the trash list.
/// </summary>
public sealed record TrashListEntry(
    string EntryId,
    string ItemId,
    TrashEntryKind Kind,
    string RelativePath,
    string? OriginalParentId,
    TrashSource Source,
    DateTimeOffset DeletionTimestamp,
    DateTimeOffset? RestoredTimestamp);

/// <summary>Kind of a trashed item.</summary>
public enum TrashEntryKind
{
    Note = 0,
    Folder = 1,
    Flashcard = 2,
    Other = 99,
}

/// <summary>
/// View-model for the Trash / Empty Trash flow. The view-model
/// is the user-facing surface of <see cref="ICollectionSafetyService"/>;
/// it never builds destructive commands directly. The
/// destructive mutation is gated by the user-typed
/// <see cref="EmptyTrashConfirmationText"/> matching the
/// expected token "EMPTY".
/// </summary>
public sealed class TrashViewModel : INotifyPropertyChanged
{
    private const string EmptyTrashToken = "EMPTY";

    private readonly ICollectionSafetyService safetyService;
    private readonly Func<TrashListEntry[]> entriesProvider;

    private TrashViewState state = TrashViewState.Idle;
    private string? lastError;
    private long collectionId;
    private string? destinationDirectory;
    private string emptyTrashConfirmationText = string.Empty;

    /// <summary>Builds a trash view-model bound to the supplied safety service and entry provider.</summary>
    public TrashViewModel(
        ICollectionSafetyService safetyService,
        Func<TrashListEntry[]> entriesProvider)
    {
        this.safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        this.entriesProvider = entriesProvider ?? throw new ArgumentNullException(nameof(entriesProvider));
        EmptyTrashCommand = new RelayCommand(
            execute: _ => _ = EmptyTrashAsync(),
            canExecute: _ => IsEmptyTrashArmed
                && State != TrashViewState.Deleting
                && State != TrashViewState.Loading);
    }

    /// <summary>Raised when a property's value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current coarse-grained state of the view-model.</summary>
    public TrashViewState State
    {
        get => state;
        private set
        {
            if (state != value)
            {
                state = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Last single-line error message, or <c>null</c> on success.</summary>
    public string? LastError
    {
        get => lastError;
        private set
        {
            if (lastError != value)
            {
                lastError = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Current collection id; supplied by the shell before <see cref="LoadAsync"/>.</summary>
    public long CollectionId
    {
        get => collectionId;
        private set
        {
            if (collectionId != value)
            {
                collectionId = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Staging destination directory used by destructive moves; null until the shell sets it.</summary>
    public string? DestinationDirectory
    {
        get => destinationDirectory;
        private set
        {
            if (destinationDirectory != value)
            {
                destinationDirectory = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Live list of trashed entries; updated by <see cref="LoadAsync"/>.</summary>
    public ObservableCollection<TrashListEntry> TrashList { get; } = new();

    /// <summary>Command bound to the Empty Trash button.</summary>
    public ICommand EmptyTrashCommand { get; }

    /// <summary>Single-line prompt the user reads before typing the confirmation token.</summary>
    public string EmptyTrashConfirmationPrompt =>
        "Type EMPTY to permanently empty the trash. This is irreversible without a verified safety backup.";

    /// <summary>User-typed confirmation text; updated by the view.</summary>
    public string EmptyTrashConfirmationText
    {
        get => emptyTrashConfirmationText;
        set
        {
            string next = value ?? string.Empty;
            if (emptyTrashConfirmationText != next)
            {
                emptyTrashConfirmationText = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmptyTrashArmed));
            }
        }
    }

    /// <summary>True when the user typed the expected token.</summary>
    public bool IsEmptyTrashArmed
        => string.Equals(emptyTrashConfirmationText.Trim(), EmptyTrashToken, StringComparison.Ordinal);

    /// <summary>Stable automation name for the Empty Trash command.</summary>
    public string EmptyTrashCommandAutomationName => "Empty trash";

    /// <summary>Stable automation help text for the Empty Trash command.</summary>
    public string EmptyTrashCommandAutomationHelpText =>
        "Permanently removes every trashed item after a verified safety backup. The user must type EMPTY.";

    /// <summary>Sets the collection id and destination directory used by destructive moves.</summary>
    public void SetEmptyTrashContext(long collectionId, string destinationDirectory)
    {
        CollectionId = collectionId;
        DestinationDirectory = destinationDirectory;
    }

    /// <summary>Loads the current trash list from the supplied provider.</summary>
    public async Task LoadAsync()
    {
        State = TrashViewState.Loading;
        try
        {
            TrashList.Clear();
            TrashListEntry[] entries = entriesProvider();
            foreach (TrashListEntry e in entries) TrashList.Add(e);
            State = TrashViewState.Ready;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = TrashViewState.Failed;
        }
    }

    /// <summary>Runs the Empty Trash flow.</summary>
    public async Task EmptyTrashAsync()
    {
        if (State == TrashViewState.Deleting) return;
        State = TrashViewState.Deleting;
        try
        {
            EmptyTrashHandle handle = safetyService.StartEmptyTrash(new EmptyTrashRequest(
                CollectionId: CollectionId,
                AcknowledgeIrreversible: IsEmptyTrashArmed));
            EmptyTrashOutcome outcome = await handle.Completion.ConfigureAwait(true);
            if (outcome.FailureKind == EmptyTrashFailureKind.None)
            {
                TrashList.Clear();
                State = TrashViewState.Ready;
                EmptyTrashConfirmationText = string.Empty;
            }
            else
            {
                LastError = $"Empty trash failed: {outcome.FailureKind}";
                State = TrashViewState.Failed;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = TrashViewState.Failed;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
