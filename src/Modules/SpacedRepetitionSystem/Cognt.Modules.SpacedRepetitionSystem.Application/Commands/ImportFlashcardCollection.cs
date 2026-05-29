using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class ImportFlashcardCollection(
    IFlashcardCollectionImportWriter flashcardCollectionImporter)
    : IImportFlashcardCollectionCommand
{
    private readonly IFlashcardCollectionImportWriter importer =
        flashcardCollectionImporter ?? throw new ArgumentNullException(nameof(flashcardCollectionImporter));

    public async Task<ImportFlashcardCollectionResult> ExecuteAsync(
        ImportFlashcardCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCollectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationCollectionId);
        ArgumentNullException.ThrowIfNull(request.SourceDocumentPathMap);

        ImportFlashcardCollectionResult result = await importer.ImportCollectionAsync(
            request.SourceCollectionId,
            request.DestinationCollectionId,
            request.SourceDocumentPathMap,
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}
