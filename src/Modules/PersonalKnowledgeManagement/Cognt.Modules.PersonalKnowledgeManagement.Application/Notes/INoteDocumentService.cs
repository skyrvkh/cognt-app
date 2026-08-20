namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

/// <summary>
/// Read-side view of a single note revision as the
/// editor workspace sees it. The model carries the
/// canonical content, the plain-text derivation, the
/// tag list, and the format tags the editor must echo
/// back when it requests the next save.
/// </summary>
/// <remarks>
/// The model is intentionally a record so the editor
/// can hold a snapshot for the duration of one save
/// round-trip; every save advances the
/// <see cref="NoteDocumentId"/> and the
/// <see cref="RevisionSha256"/>.
/// </remarks>
public sealed record NoteContentReadModel(
    string NoteDocumentId,
    long NoteItemId,
    byte[] Content,
    string PlainText,
    string Tags,
    string ContentFormat,
    string SourceKind,
    string RevisionSha256,
    DateTimeOffset SavedAtUtc);

/// <summary>
/// Document-centric note service the WPF workspace
/// uses. The contract is intentionally narrow: the
/// workspace only needs the latest canonical content
/// and a single transactional save. The broader
/// collection-wide <see cref="INoteService"/> surface
/// stays separate so the WPF session does not have to
/// be aware of it.
/// </summary>
public interface INoteDocumentService
{
    /// <summary>
    /// Loads the latest canonical content for the
    /// supplied note item. Returns <c>null</c> when
    /// the note has no recorded content yet.
    /// </summary>
    /// <param name="noteItemId">Identifier of the note item to load.</param>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    Task<NoteContentReadModel?> LoadLatestNoteContentAsync(
        long noteItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the supplied save delegate inside a single
    /// SQLite transaction. The delegate is given the
    /// same <see cref="INoteDocumentService"/> instance
    /// so the save can issue a single
    /// <see cref="SaveNoteContentAsync"/> call; the
    /// transaction is committed when the delegate
    /// returns and rolled back when the delegate
    /// throws.
    /// </summary>
    /// <param name="saveDelegate">Delegate that performs the save.</param>
    /// <param name="cancellationToken">Token used to cancel the save.</param>
    Task<NoteContentReadModel> RunInTransactionAsync(
        Func<INoteDocumentService, CancellationToken, Task<NoteContentReadModel>> saveDelegate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied content as a new revision
    /// of the note. The implementation assigns the new
    /// <see cref="NoteContentReadModel.NoteDocumentId"/>
    /// and <see cref="NoteContentReadModel.RevisionSha256"/>
    /// and returns the resulting read model.
    /// </summary>
    /// <param name="noteItemId">Identifier of the note item to save.</param>
    /// <param name="expectedNoteDocumentId">Currently-bound note document id; pass an empty string for the first save.</param>
    /// <param name="content">UTF-8 content bytes to persist.</param>
    /// <param name="plainText">Plain-text derivation of the content.</param>
    /// <param name="tags">Space-joined tag names, lower case.</param>
    /// <param name="contentFormat">Format tag (for example <c>"markdown"</c>).</param>
    /// <param name="sourceKind">Origin tag (for example <c>"user_save"</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the save.</param>
    Task<NoteContentReadModel> SaveNoteContentAsync(
        long noteItemId,
        string expectedNoteDocumentId,
        byte[] content,
        string plainText,
        string tags,
        string contentFormat,
        string sourceKind,
        CancellationToken cancellationToken = default);
}
