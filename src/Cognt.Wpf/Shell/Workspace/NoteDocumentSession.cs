using System.ComponentModel;
using System.Runtime.CompilerServices;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.Shell.Workspace;

/// <summary>
/// Lifecycle state of a single note document session.
/// The state machine is monotonic within one session:
/// <see cref="Idle"/> ÔåÆ <see cref="Loading"/> ÔåÆ
/// <see cref="Ready"/> ÔåÆ <see cref="Saving"/> ÔåÆ
/// <see cref="Ready"/>. A failure or cancellation can
/// drop the session back to <see cref="Ready"/> (with
/// a sticky failure) or <see cref="Disposed"/>; once
/// disposed the session cannot be reused.
/// </summary>
public enum NoteDocumentSessionState
{
    /// <summary>Session has no active note loaded.</summary>
    Idle,

    /// <summary>Session is loading the active note from the canonical store.</summary>
    Loading,

    /// <summary>Session is bound to a note revision and ready for input.</summary>
    Ready,

    /// <summary>Session is saving the dirty state to the canonical store.</summary>
    Saving,

    /// <summary>Session is disposed; no further commands may be issued.</summary>
    Disposed,
}

/// <summary>
/// Coarse-grained outcome the session publishes for
/// each save attempt. Mirrors the editor
/// <see cref="CogntApp.Shell.EditorNoteSaveOutcome"/>
/// taxonomy and adds the cancellation branch.
/// </summary>
public enum NoteDocumentSaveOutcome
{
    /// <summary>No save has been attempted in the current session.</summary>
    None,

    /// <summary>A save is currently in progress.</summary>
    Saving,

    /// <summary>The most recent save committed.</summary>
    Saved,

    /// <summary>The most recent save was cancelled before commit.</summary>
    Cancelled,

    /// <summary>The most recent save failed; the dirty state is preserved.</summary>
    Failed,
}

/// <summary>
/// Single note document session: the typed bridge
/// between the WPF workspace, the embedded editor, and
/// the application-layer note document service. The
/// session is the sole session-state owner: every
/// bridge message is typed, bounded, correlated,
/// generation-checked, and safe after disposal.
/// </summary>
/// <remarks>
/// The session is created by the workspace; once
/// disposed the session cannot be reused. The session
/// exposes a single
/// <see cref="Generation"/> counter that the editor
/// must echo back on every callback ÔÇö stale callbacks
/// (with a generation less than the current value) are
/// dropped before they can mutate a newer session.
/// </remarks>
public sealed partial class NoteDocumentSession : INotifyPropertyChanged
{
    private readonly INoteDocumentService documentService;
    private readonly object generationGate = new();
    private long noteItemId;
    private string noteDocumentId = string.Empty;
    private long generation;
    private NoteDocumentSessionState state = NoteDocumentSessionState.Idle;
    private NoteDocumentSaveOutcome saveOutcome = NoteDocumentSaveOutcome.None;
    private string lastErrorMessage = string.Empty;
    private bool isDirty;
    private bool isDisposed;

    /// <summary>
    /// Raised whenever a public property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Stable note item id the session is bound to.
    /// Zero before the first
    /// <see cref="BindAsync"/> call.
    /// </summary>
    public long NoteItemId
    {
        get
        {
            lock (generationGate)
            {
                return noteItemId;
            }
        }
    }

    /// <summary>
    /// Stable note document id the session is bound
    /// to. Empty before the first
    /// <see cref="BindAsync"/> call.
    /// </summary>
    public string NoteDocumentId
    {
        get
        {
            lock (generationGate)
            {
                return noteDocumentId;
            }
        }
    }

    /// <summary>
    /// Monotonically increasing generation. Every
    /// successful <see cref="BindAsync"/> or
    /// <see cref="SaveAsync"/> bumps the counter; the
    /// editor must echo the value back on every
    /// callback and the session drops any callback
    /// whose generation does not match.
    /// </summary>
    public long Generation
    {
        get
        {
            lock (generationGate)
            {
                return generation;
            }
        }
    }

    /// <summary>
    /// Current lifecycle state. The shell binds the
    /// value to status badges and disables input while
    /// the session is in <see cref="NoteDocumentSessionState.Loading"/>
    /// or <see cref="NoteDocumentSessionState.Saving"/>.
    /// </summary>
    public NoteDocumentSessionState State
    {
        get
        {
            lock (generationGate)
            {
                return state;
            }
        }
    }

    /// <summary>
    /// Last save outcome. Defaults to
    /// <see cref="NoteDocumentSaveOutcome.None"/>.
    /// </summary>
    public NoteDocumentSaveOutcome SaveOutcome
    {
        get
        {
            lock (generationGate)
            {
                return saveOutcome;
            }
        }
    }

    /// <summary>
    /// Short, single-line error message from the most
    /// recent failure. Always non-null; the empty
    /// string is the "no failure" sentinel.
    /// </summary>
    public string LastErrorMessage
    {
        get
        {
            lock (generationGate)
            {
                return lastErrorMessage;
            }
        }
    }

    /// <summary>
    /// True when the editor has unsaved changes. The
    /// shell disables navigation when
    /// <see cref="IsDirty"/> is <c>true</c>.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            lock (generationGate)
            {
                return isDirty;
            }
        }
    }

    /// <summary>
    /// True when the session is disposed; no further
    /// commands may be issued.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (generationGate)
            {
                return isDisposed;
            }
        }
    }

    /// <summary>
    /// True when the session can accept a new save
    /// command. False while a save is in flight or
    /// the session is disposed.
    /// </summary>
    public bool CanSave
    {
        get
        {
            lock (generationGate)
            {
                return !isDisposed
                    && state == NoteDocumentSessionState.Ready
                    && saveOutcome != NoteDocumentSaveOutcome.Saving
                    && isDirty;
            }
        }
    }

    /// <summary>
    /// Creates a new session backed by the supplied
    /// note document service. The session is created
    /// in <see cref="NoteDocumentSessionState.Idle"/>
    /// with generation 0.
    /// </summary>
    /// <param name="documentService">Note document service.</param>
    public NoteDocumentSession(INoteDocumentService documentService)
    {
        ArgumentNullException.ThrowIfNull(documentService);
        this.documentService = documentService;
    }

    /// <summary>
    /// Binds the session to the supplied note item,
    /// bumps the generation counter, and loads the
    /// latest canonical content. Stale callbacks
    /// (with a generation less than the post-bind
    /// value) are dropped before they can mutate a
    /// newer session.
    /// </summary>
    /// <param name="noteItemId">Identifier of the note item.</param>
    /// <param name="cancellationToken">Token used to cancel the bind.</param>
    /// <returns>
    /// The loaded note content read model, or
    /// <c>null</c> when the note has no content yet.
    /// </returns>
    public async Task<NoteContentReadModel?> BindAsync(
        long noteItemId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        long expectedGeneration;
        lock (generationGate)
        {
            state = NoteDocumentSessionState.Loading;
            isDirty = false;
            this.noteItemId = noteItemId;
            this.generation++;
            expectedGeneration = this.generation;
        }
        RaiseAll();

        try
        {
            NoteContentReadModel? content = await documentService
                .LoadLatestNoteContentAsync(noteItemId, cancellationToken)
                .ConfigureAwait(false);

            lock (generationGate)
            {
                if (expectedGeneration != this.generation)
                {
                    // Stale callback: a newer bind overrode
                    // us. Drop this result.
                    return null;
                }

                this.noteDocumentId = content?.NoteDocumentId ?? string.Empty;
                state = NoteDocumentSessionState.Ready;
                saveOutcome = NoteDocumentSaveOutcome.None;
                lastErrorMessage = string.Empty;
            }

            RaiseAll();
            return content;
        }
        catch (OperationCanceledException)
        {
            lock (generationGate)
            {
                if (expectedGeneration == this.generation)
                {
                    state = NoteDocumentSessionState.Idle;
                    saveOutcome = NoteDocumentSaveOutcome.Cancelled;
                    lastErrorMessage = "The note load was cancelled.";
                }
            }
            RaiseAll();
            throw;
        }
    }

    /// <summary>
    /// Marks the session dirty. The session bumps its
    /// generation so the editor must re-snapshot the
    /// active content.
    /// </summary>
    public void MarkDirty()
    {
        EnsureNotDisposed();
        lock (generationGate)
        {
            if (state == NoteDocumentSessionState.Ready && !isDirty)
            {
                isDirty = true;
            }
        }
        Raise(nameof(IsDirty));
        Raise(nameof(CanSave));
    }

    /// <summary>
    /// Saves the supplied content to the canonical
    /// store inside one transaction, advances the
    /// generation, and clears the dirty flag. The
    /// save is the single mutation point the editor
    /// is allowed to drive.
    /// </summary>
    /// <param name="content">UTF-8 content bytes to save.</param>
    /// <param name="plainText">Plain-text derivation of the content.</param>
    /// <param name="tags">Space-joined tag names, lower case.</param>
    /// <param name="contentFormat">Format tag (e.g. <c>"markdown"</c>).</param>
    /// <param name="sourceKind">Origin tag (e.g. <c>"user_save"</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the save.</param>
    public async Task<NoteContentReadModel> SaveAsync(
        byte[] content,
        string plainText,
        string tags,
        string contentFormat,
        string sourceKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(contentFormat);
        ArgumentNullException.ThrowIfNull(sourceKind);
        EnsureNotDisposed();

        long expectedGeneration;
        long currentNoteItemId;
        string currentNoteDocumentId;
        lock (generationGate)
        {
            if (state != NoteDocumentSessionState.Ready)
            {
                throw new InvalidOperationException(
                    $"Cannot save a session in state {state}; bind the session first.");
            }

            state = NoteDocumentSessionState.Saving;
            saveOutcome = NoteDocumentSaveOutcome.Saving;
            lastErrorMessage = string.Empty;
            this.generation++;
            expectedGeneration = this.generation;
            currentNoteItemId = this.noteItemId;
            currentNoteDocumentId = this.noteDocumentId;
        }
        RaiseAll();

        try
        {
            NoteContentReadModel saved = await documentService
                .RunInTransactionAsync(
                    (svc, ct) => svc.SaveNoteContentAsync(
                        currentNoteItemId,
                        currentNoteDocumentId,
                        content,
                        plainText,
                        tags,
                        contentFormat,
                        sourceKind,
                        ct),
                    cancellationToken)
                .ConfigureAwait(false);

            lock (generationGate)
            {
                if (expectedGeneration != this.generation)
                {
                    return saved;
                }

                this.noteDocumentId = saved.NoteDocumentId;
                state = NoteDocumentSessionState.Ready;
                saveOutcome = NoteDocumentSaveOutcome.Saved;
                isDirty = false;
                lastErrorMessage = string.Empty;
            }
            RaiseAll();
            return saved;
        }
        catch (OperationCanceledException)
        {
            lock (generationGate)
            {
                if (expectedGeneration == this.generation)
                {
                    state = NoteDocumentSessionState.Ready;
                    saveOutcome = NoteDocumentSaveOutcome.Cancelled;
                    lastErrorMessage = "The save was cancelled before it could commit.";
                }
            }
            RaiseAll();
            throw;
        }
        catch (Exception ex)
        {
            lock (generationGate)
            {
                if (expectedGeneration == this.generation)
                {
                    state = NoteDocumentSessionState.Ready;
                    saveOutcome = NoteDocumentSaveOutcome.Failed;
                    lastErrorMessage = ex.Message;
                }
            }
            RaiseAll();
            throw;
        }
    }

    /// <summary>
    /// Disposes the session. After disposal no
    /// further commands may be issued. The session
    /// cannot be reused.
    /// </summary>
    public void Dispose()
    {
        lock (generationGate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            state = NoteDocumentSessionState.Disposed;
        }
        RaiseAll();
    }

    private void EnsureNotDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(NoteDocumentSession));
        }
    }

    private void RaiseAll()
    {
        Raise(nameof(NoteItemId));
        Raise(nameof(NoteDocumentId));
        Raise(nameof(Generation));
        Raise(nameof(State));
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(IsDirty));
        Raise(nameof(IsDisposed));
        Raise(nameof(CanSave));
    }

    private void Raise([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

