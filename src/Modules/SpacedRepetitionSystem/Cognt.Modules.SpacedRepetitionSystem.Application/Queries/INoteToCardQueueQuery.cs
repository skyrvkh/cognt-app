using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface INoteToCardQueueQuery
{
    NoteToCardQueueReadModel Execute(NoteToCardQueueQueryRequest request);
}

public sealed record NoteToCardQueueQueryRequest(
    string CollectionId,
    string SourceRelPath,
    string NoteContent,
    CardContextDraftNoteDocument DraftDocument);
