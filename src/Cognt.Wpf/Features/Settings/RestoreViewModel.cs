using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using CogntApp.ViewModels;

namespace CogntApp.Features.Settings;

/// <summary>
/// Coarse-grained state of the restore view-model.
/// </summary>
public enum RestoreSettingsState
{
    /// <summary>No restore is in progress.</summary>
    Idle = 0,
    /// <summary>The user is selecting an archive.</summary>
    Selecting = 1,
    /// <summary>The user is selecting a target directory.</summary>
    SelectingTarget = 2,
    /// <summary>A restore is in progress; the typed state mirrors <see cref="RestoreStage"/>.</summary>
    Restoring = 3,
    /// <summary>The last restore committed successfully.</summary>
    Succeeded = 4,
    /// <summary>The last restore failed; the source archive is untouched.</summary>
    Failed = 5,
}

/// <summary>
/// View-model for the Restore settings page. The view-model
/// exposes the typed progress surface (<see cref="RestoreStage"/>)
/// and the user-typed source archive / target directory. The
/// destructive pipeline is hosted by
/// <see cref="RestoreArchiveService"/>; the view-model only
/// consumes the typed outcome.
/// </summary>
public sealed class RestoreViewModel : INotifyPropertyChanged
{
    private readonly ICollectionSafetyService safetyService;
    private readonly Func<long> collectionIdProvider;
    private readonly Func<string?> archivePicker;
    private readonly Func<string?> targetPicker;

    private RestoreSettingsState state = RestoreSettingsState.Idle;
    private string? lastError;
    private string? archivePath;
    private string? targetCollectionRoot;
    private string? lastRestoredCollectionRoot;
    private string stage = "Idle";
    private string? openError;

    /// <summary>Builds a restore view-model bound to the supplied safety service and pickers.</summary>
    public RestoreViewModel(
        ICollectionSafetyService safetyService,
        Func<long> collectionIdProvider,
        Func<string?>? archivePicker = null,
        Func<string?>? targetPicker = null)
    {
        this.safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        this.collectionIdProvider = collectionIdProvider ?? throw new ArgumentNullException(nameof(collectionIdProvider));
        this.archivePicker = archivePicker ?? (() => null);
        this.targetPicker = targetPicker ?? (() => null);
        PickArchiveCommand = new RelayCommand(
            execute: _ => _ = PickArchiveAsync(),
            canExecute: _ => State != RestoreSettingsState.Restoring);
        PickTargetCommand = new RelayCommand(
            execute: _ => _ = PickTargetAsync(),
            canExecute: _ => State != RestoreSettingsState.Restoring);
        RestoreNowCommand = new RelayCommand(
            execute: _ => _ = RestoreNowAsync(),
            canExecute: _ => IsSourceAndTargetValid
                && State != RestoreSettingsState.Restoring);
        OpenRestoredCommand = new RelayCommand(
            execute: _ => _ = OpenRestoredAsync(),
            canExecute: _ => !string.IsNullOrEmpty(LastRestoredCollectionRoot));
    }

    /// <summary>Raised when a property's value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current coarse-grained state.</summary>
    public RestoreSettingsState State
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

    /// <summary>User-typed source archive path.</summary>
    public string? ArchivePath
    {
        get => archivePath;
        set
        {
            if (archivePath != value)
            {
                archivePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSourceAndTargetValid));
            }
        }
    }

    /// <summary>User-typed target collection directory.</summary>
    public string? TargetCollectionRoot
    {
        get => targetCollectionRoot;
        set
        {
            if (targetCollectionRoot != value)
            {
                targetCollectionRoot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSourceAndTargetValid));
                OnPropertyChanged(nameof(IsTargetValid));
            }
        }
    }

    /// <summary>Path of the last successfully restored collection.</summary>
    public string? LastRestoredCollectionRoot
    {
        get => lastRestoredCollectionRoot;
        private set
        {
            if (lastRestoredCollectionRoot != value)
            {
                lastRestoredCollectionRoot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasRestoredCollection));
            }
        }
    }

    /// <summary>Last reported <see cref="RestoreStage"/>.</summary>
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

    /// <summary>Last single-line open error.</summary>
    public string? OpenError
    {
        get => openError;
        private set
        {
            if (openError != value)
            {
                openError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasOpenError));
            }
        }
    }

    /// <summary>True when source archive and target are both set.</summary>
    public bool IsSourceAndTargetValid
        => !string.IsNullOrWhiteSpace(ArchivePath)
            && !string.IsNullOrWhiteSpace(TargetCollectionRoot);

    /// <summary>True when the target directory is a non-empty rooted path.</summary>
    public bool IsTargetValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TargetCollectionRoot)) return false;
            try { return Path.IsPathRooted(TargetCollectionRoot); }
            catch { return false; }
        }
    }

    /// <summary>True when the last restore left a registered path.</summary>
    public bool HasRestoredCollection => !string.IsNullOrEmpty(LastRestoredCollectionRoot);

    /// <summary>True when an open error is set.</summary>
    public bool HasOpenError => !string.IsNullOrEmpty(OpenError);

    /// <summary>True when a restore is in progress.</summary>
    public bool IsBusy => state == RestoreSettingsState.Restoring;

    /// <summary>True when no restore is in progress.</summary>
    public bool IsIdle => state == RestoreSettingsState.Idle;

    /// <summary>Command bound to the Pick Archive button.</summary>
    public ICommand PickArchiveCommand { get; }

    /// <summary>Command bound to the Pick Target button.</summary>
    public ICommand PickTargetCommand { get; }

    /// <summary>Command bound to the Restore Now button.</summary>
    public ICommand RestoreNowCommand { get; }

    /// <summary>Command bound to the Open Restored button.</summary>
    public ICommand OpenRestoredCommand { get; }

    /// <summary>Stable automation name for the Restore action.</summary>
    public string RestoreCommandAutomationName => "Restore collection";

    /// <summary>Stable automation help text for the Restore action.</summary>
    public string RestoreCommandAutomationHelpText =>
        "Restores a collection from a backup archive. The archive and the target directory are required.";

    /// <summary>Stable automation name for the Open Restored action.</summary>
    public string OpenRestoredCommandAutomationName => "Open restored collection";

    /// <summary>Stable automation help text for the Open Restored action.</summary>
    public string OpenRestoredCommandAutomationHelpText =>
        "Opens the most recently restored collection in the active workspace.";

    private async Task PickArchiveAsync()
    {
        if (State == RestoreSettingsState.Restoring) return;
        State = RestoreSettingsState.Selecting;
        try
        {
            string? picked = archivePicker();
            if (!string.IsNullOrEmpty(picked)) ArchivePath = picked;
        }
        finally
        {
            State = RestoreSettingsState.Idle;
        }
    }

    private async Task PickTargetAsync()
    {
        if (State == RestoreSettingsState.Restoring) return;
        State = RestoreSettingsState.SelectingTarget;
        try
        {
            string? picked = targetPicker();
            if (!string.IsNullOrEmpty(picked)) TargetCollectionRoot = picked;
        }
        finally
        {
            State = RestoreSettingsState.Idle;
        }
    }

    private async Task RestoreNowAsync()
    {
        if (!IsSourceAndTargetValid)
        {
            LastError = "Archive and target are required.";
            State = RestoreSettingsState.Failed;
            return;
        }
        State = RestoreSettingsState.Restoring;
        Stage = "Validated";
        try
        {
            RestoreHandle handle = safetyService.StartRestore(new RestoreRequest(
                CollectionId: collectionIdProvider(),
                ArchivePath: ArchivePath!,
                TargetCollectionRoot: TargetCollectionRoot!,
                VerifyBeforeExtract: true));
            RestoreOutcome outcome = await handle.Completion.ConfigureAwait(true);
            if (outcome.FailureKind == RestoreFailureKind.None)
            {
                LastRestoredCollectionRoot = TargetCollectionRoot;
                Stage = "Registered";
                State = RestoreSettingsState.Succeeded;
            }
            else
            {
                LastError = $"Restore failed: {outcome.FailureKind}";
                State = RestoreSettingsState.Failed;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = RestoreSettingsState.Failed;
        }
    }

    private async Task OpenRestoredAsync()
    {
        if (string.IsNullOrEmpty(LastRestoredCollectionRoot)) return;
        if (!Directory.Exists(LastRestoredCollectionRoot))
        {
            OpenError = "The restored collection is missing.";
            return;
        }
        OpenError = null;
    }

    /// <summary>Clears the restored-collection surface after the shell has opened it.</summary>
    public void ClearRestoredCollection()
    {
        LastRestoredCollectionRoot = null;
        OpenError = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
