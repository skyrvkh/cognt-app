using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardCollectionImportWriter
{
    Task<ImportFlashcardCollectionResult> ImportCollectionAsync(
        string sourceCollectionId,
        string destinationCollectionId,
        IReadOnlyDictionary<string, string> sourceDocumentPathMap,
        CancellationToken cancellationToken = default);
}
