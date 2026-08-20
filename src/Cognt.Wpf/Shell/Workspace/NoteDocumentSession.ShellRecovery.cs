using System;
using System.ComponentModel;

namespace CogntApp.Shell.Workspace;

/// <summary>
/// M4-29 SM29-12 partial of
/// <see cref="NoteDocumentSession"/>: shell-level
/// failure-recovery surface that the workspace binds
/// to without reaching into the editor host. The save
/// state machine and the generation counter live on the
/// main session file; this partial owns the typed
/// in-flight content, the committed-hash record, and the
/// explicit user-initiated resume / discard commands.
/// </summary>
public sealed partial class NoteDocumentSession
{
    private string? pendingShellDocumentId;
    private string pendingShellContent = string.Empty;
    private DateTimeOffset shellRecoveryRecordedAt;
    private string? shellLastError;

    /// <summary>
    /// Current shell-level recovery. Non-null only
    /// after a <see cref="BeginSave"/> call that has
    /// not been resolved by <see cref="CommitSave"/>,
    /// <see cref="FailSave"/>, or
    /// <see cref="DiscardRecovery"/>. The shell binds
    /// the value to the recovery surface.
    /// </summary>
    public NoteDocumentRecovery? ShellRecovery
    {
        get
        {
            lock (generationGate)
            {
                return pendingShellDocumentId is null
                    ? null
                    : new NoteDocumentRecovery(
                        DocumentId: pendingShellDocumentId,
                        PendingContent: pendingShellContent,
                        FailureReason: shellLastError,
                        RecoveredAt: shellRecoveryRecordedAt);
            }
        }
    }

    /// <summary>
    /// True when a shell-level recovery is pending.
    /// The shell uses this to decide whether to surface
    /// a "Recover your last edit?" prompt.
    /// </summary>
    public bool HasShellRecovery
    {
        get
        {
            lock (generationGate)
            {
                return pendingShellDocumentId is not null;
            }
        }
    }

    /// <summary>
    /// Begins a shell-level save attempt. Records the
    /// in-flight content so a failure can be recovered,
    /// transitions the save outcome to
    /// <see cref="NoteDocumentSaveOutcome.Saving"/>, and
    /// clears the previous error. Throws
    /// <see cref="ArgumentException"/> when
    /// <paramref name="documentId"/> is null or
    /// whitespace, and
    /// <see cref="ArgumentNullException"/> when
    /// <paramref name="content"/> is null.
    /// </summary>
    /// <param name="documentId">Identifier of the document.</param>
    /// <param name="content">User-entered content to save.</param>
    public void BeginSave(string documentId, string content)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException(
                "Document id must be a non-empty, non-whitespace value.",
                nameof(documentId));
        }
        ArgumentNullException.ThrowIfNull(content);

        lock (generationGate)
        {
            pendingShellDocumentId = documentId;
            pendingShellContent = content;
            shellRecoveryRecordedAt = DateTimeOffset.UtcNow;
            shellLastError = null;
            saveOutcome = NoteDocumentSaveOutcome.Saving;
            lastErrorMessage = string.Empty;
        }
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(ShellRecovery));
        Raise(nameof(HasShellRecovery));
    }

    /// <summary>
    /// Commits a successful shell-level save. Records
    /// the committed hash and clears the in-flight
    /// recovery slot. Throws
    /// <see cref="ArgumentNullException"/> when
    /// <paramref name="contentSha256"/> is null.
    /// </summary>
    /// <param name="contentSha256">Lowercase hex SHA-256 of the committed content.</param>
    public void CommitSave(string contentSha256)
    {
        ArgumentNullException.ThrowIfNull(contentSha256);

        lock (generationGate)
        {
            pendingShellDocumentId = null;
            pendingShellContent = string.Empty;
            shellLastError = null;
            saveOutcome = NoteDocumentSaveOutcome.Saved;
            lastErrorMessage = string.Empty;
        }
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(ShellRecovery));
        Raise(nameof(HasShellRecovery));
    }

    /// <summary>
    /// Records a failed shell-level save. Preserves the
    /// in-flight content and the document id so the
    /// caller can <see cref="Resume"/> the same
    /// content; transitions the save outcome to
    /// <see cref="NoteDocumentSaveOutcome.Failed"/>.
    /// Throws <see cref="ArgumentNullException"/> when
    /// <paramref name="reason"/> is null.
    /// </summary>
    /// <param name="reason">Single-line failure reason.</param>
    public void FailSave(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        lock (generationGate)
        {
            shellLastError = reason;
            saveOutcome = NoteDocumentSaveOutcome.Failed;
            lastErrorMessage = reason;
            shellRecoveryRecordedAt = DateTimeOffset.UtcNow;
        }
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(ShellRecovery));
        Raise(nameof(HasShellRecovery));
    }

    /// <summary>
    /// Resumes the document session from the recorded
    /// shell recovery. Returns the in-flight content to
    /// the caller and clears the recovery slot. Returns
    /// <c>null</c> when no shell recovery is in flight.
    /// The method never throws.
    /// </summary>
    /// <returns>The recovered content, or <c>null</c> when no recovery is in flight.</returns>
    public string? Resume()
    {
        string? content;
        lock (generationGate)
        {
            if (pendingShellDocumentId is null)
            {
                return null;
            }
            content = pendingShellContent;
            pendingShellDocumentId = null;
            pendingShellContent = string.Empty;
            saveOutcome = NoteDocumentSaveOutcome.Saving;
            lastErrorMessage = string.Empty;
        }
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(ShellRecovery));
        Raise(nameof(HasShellRecovery));
        return content;
    }

    /// <summary>
    /// Discards the shell recovery. Returns the
    /// session to <see cref="NoteDocumentSaveOutcome.None"/>
    /// and clears the in-flight content. The method
    /// never throws.
    /// </summary>
    public void DiscardRecovery()
    {
        lock (generationGate)
        {
            pendingShellDocumentId = null;
            pendingShellContent = string.Empty;
            shellLastError = null;
            saveOutcome = NoteDocumentSaveOutcome.None;
            lastErrorMessage = string.Empty;
        }
        Raise(nameof(SaveOutcome));
        Raise(nameof(LastErrorMessage));
        Raise(nameof(ShellRecovery));
        Raise(nameof(HasShellRecovery));
    }

    /// <summary>Stable automation name for the save action.</summary>
    public string SaveCommandAutomationName => "Save document";

    /// <summary>Stable automation help text for the save action.</summary>
    public string SaveCommandAutomationHelpText =>
        "Saves the current document. On failure, the in-flight content is preserved for recovery.";
}

/// <summary>
/// Recovery record for a failed or interrupted save
/// at the shell level. The shell binds the value to
/// the recovery surface; the editor uses the pending
/// content to restore the dirty state when the user
/// reopens the note.
/// </summary>
/// <param name="DocumentId">Identifier of the document the save was attempted on.</param>
/// <param name="PendingContent">User-entered content that has not been committed.</param>
/// <param name="FailureReason">Single-line reason the save failed.</param>
/// <param name="RecoveredAt">UTC timestamp the recovery was recorded.</param>
public sealed record NoteDocumentRecovery(
    string DocumentId,
    string PendingContent,
    string? FailureReason,
    DateTimeOffset RecoveredAt);
