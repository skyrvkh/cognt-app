using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using CogntApp.ViewModels;

namespace CogntApp.Features.Settings;

/// <summary>
/// Coarse-grained state of the backup view-model.
/// </summary>
public enum BackupSettingsState
{
    /// <summary>No backup is currently in progress.</summary>
    Idle = 0,
    /// <summary>The user is selecting a destination.</summary>
    Selecting = 1,
    /// <summary>A backup is in progress; the typed state mirrors <see cref="BackupStage"/>.</summary>
    BackingUp = 2,
    /// <summary>The last backup committed successfully.</summary>
    Succeeded = 3,
    /// <summary>The last backup failed; the source collection is untouched.</summary>
    Failed = 4,
    /// <summary>The user cancelled the backup.</summary>
    Cancelled = 5,
}

/// <summary>
/// View-model for the Backup settings page. The view-model
/// exposes the typed progress surface
/// (<see cref="BackupStage"/>) and the user-typed destination
/// directory. The destructive mutation is gated by the
/// verified safety backup in <see cref="BackupArchiveService"/>;
/// the view-model only consumes the typed outcome.
/// </summary>
public sealed class BackupSettingsViewModel : INotifyPropertyChanged
{
    private readonly ICollectionSafetyService safetyService;
    private readonly Func<long> collectionIdProvider;
    private readonly Func<string> sourceRootProvider;
    private readonly Func<string?> destinationPicker;

    private BackupSettingsState state = BackupSettingsState.Idle;
    private string? lastError;
    private string? destinationPath;
    private string? lastArchivePath;
    private string stage = "Idle";
    private long itemsCopied;
    private long itemsTotal;

    /// <summary>
    /// Builds a backup view-model bound to the supplied safety
    /// service and the three context providers.
    /// </summary>
    public BackupSettingsViewModel(
        ICollectionSafetyService safetyService,
        Func<long> collectionIdProvider,
        Func<string> sourceRootProvider,
        Func<string?>? destinationPicker = null)
    {
        this.safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        this.collectionIdProvider = collectionIdProvider ?? throw new ArgumentNullException(nameof(collectionIdProvider));
        this.sourceRootProvider = sourceRootProvider ?? throw new ArgumentNullException(nameof(sourceRootProvider));
        this.destinationPicker = destinationPicker ?? (() => null);
        BackupNowCommand = new RelayCommand(
            execute: _ => _ = BackupNowAsync(),
            canExecute: _ => !string.IsNullOrWhiteSpace(DestinationPath)
                && State != BackupSettingsState.BackingUp);
        CancelCommand = new RelayCommand(
            execute: _ => Cancel(),
            canExecute: _ => State == BackupSettingsState.BackingUp);
        PickDestinationCommand = new RelayCommand(
            execute: _ => _ = PickDestinationAsync(),
            canExecute: _ => State != BackupSettingsState.BackingUp);
    }

    /// <summary>Raised when a property's value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current coarse-grained state.</summary>
    public BackupSettingsState State
    {
        get => state;
        private set
        {
            if (state != value)
            {
                state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    /// <summary>Last single-line error message.</summary>
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

    /// <summary>User-typed destination archive path.</summary>
    public string? DestinationPath
    {
        get => destinationPath;
        set
        {
            if (destinationPath != value)
            {
                destinationPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDestinationValid));
                OnPropertyChanged(nameof(HasDestination));
            }
        }
    }

    /// <summary>Path of the last successfully committed archive.</summary>
    public string? LastArchivePath
    {
        get => lastArchivePath;
        private set
        {
            if (lastArchivePath != value)
            {
                lastArchivePath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Last reported <see cref="BackupStage"/>.</summary>
    public string Stage
    {
        get => stage;
        private set
        {
            if (stage != value)
            {
                stage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Items copied so far in the current / last run.</summary>
    public long ItemsCopied
    {
        get => itemsCopied;
        private set
        {
            if (itemsCopied != value)
            {
                itemsCopied = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Total items expected in the current / last run.</summary>
    public long ItemsTotal
    {
        get => itemsTotal;
        private set
        {
            if (itemsTotal != value)
            {
                itemsTotal = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when the destination is a non-empty, rooted path.</summary>
    public bool IsDestinationValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DestinationPath)) return false;
            try
            {
                return Path.IsPathRooted(DestinationPath);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>True when a backup is in progress.</summary>
    public bool IsBusy => state == BackupSettingsState.BackingUp;

    /// <summary>True when no backup is in progress.</summary>
    public bool IsIdle => state == BackupSettingsState.Idle;

    /// <summary>True when a destination is set.</summary>
    public bool HasDestination => !string.IsNullOrWhiteSpace(DestinationPath);

    /// <summary>List of past archives (most recent first).</summary>
    public ObservableCollection<string> ArchiveHistory { get; } = new();

    /// <summary>Command bound to the Backup Now button.</summary>
    public ICommand BackupNowCommand { get; }

    /// <summary>Command bound to the Cancel button.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Command bound to the Pick Destination button.</summary>
    public ICommand PickDestinationCommand { get; }

    /// <summary>Stable automation name for the Backup action.</summary>
    public string BackupCommandAutomationName => "Back up collection";

    /// <summary>Stable automation help text for the Backup action.</summary>
    public string BackupCommandAutomationHelpText =>
        "Creates a verified backup of the active collection. The destination is required.";

    /// <summary>Stable automation name for the Cancel action.</summary>
    public string CancelCommandAutomationName => "Cancel backup";

    /// <summary>Stable automation help text for the Cancel action.</summary>
    public string CancelCommandAutomationHelpText => "Cancels the in-flight backup.";

    private async Task PickDestinationAsync()
    {
        if (State == BackupSettingsState.BackingUp) return;
        State = BackupSettingsState.Selecting;
        try
        {
            string? picked = destinationPicker();
            if (!string.IsNullOrEmpty(picked)) DestinationPath = picked;
        }
        finally
        {
            State = BackupSettingsState.Idle;
        }
    }

    private void Cancel()
    {
        // The cancellation flow cooperates with the executor
        // through the typed handle; the view-model does not own
        // a manual cancellation token in this minimal surface.
        State = BackupSettingsState.Cancelled;
    }

    private async Task BackupNowAsync()
    {
        if (!IsDestinationValid)
        {
            LastError = "Destination is required.";
            State = BackupSettingsState.Failed;
            return;
        }
        State = BackupSettingsState.BackingUp;
        Stage = "Validated";
        try
        {
            BackupHandle handle = safetyService.StartBackup(new BackupRequest(
                CollectionId: collectionIdProvider(),
                SourceCollectionRoot: sourceRootProvider(),
                DestinationArchivePath: DestinationPath!,
                VerifyAfterWrite: true));
            BackupOutcome outcome = await handle.Completion.ConfigureAwait(true);
            if (outcome.FailureKind == BackupFailureKind.None && !string.IsNullOrEmpty(outcome.ArchivePath))
            {
                LastArchivePath = outcome.ArchivePath;
                ArchiveHistory.Insert(0, outcome.ArchivePath);
                Stage = "AtomicFinalized";
                State = BackupSettingsState.Succeeded;
            }
            else if (outcome.FailureKind == BackupFailureKind.Cancelled)
            {
                State = BackupSettingsState.Cancelled;
            }
            else
            {
                LastError = $"Backup failed: {outcome.FailureKind}";
                State = BackupSettingsState.Failed;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = BackupSettingsState.Failed;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
