using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    KnowledgeCollectionImportReadModel INoteService.ImportExistingCollection(
        string collectionId,
        string sourceCollectionId)
    {
        string destinationRoot = GetExistingCollectionRoot(collectionId);
        string sourceRoot = GetExistingCollectionRoot(sourceCollectionId);
        EnsureImportableCollectionRoots(destinationRoot, sourceRoot);

        SourceCollectionSnapshot snapshot = CaptureSourceCollectionSnapshot(sourceRoot);
        string importTitle = CreateImportedCollectionTitle(sourceRoot);
        if (snapshot.RootItems.Count == 0 && snapshot.AttachmentsById.Count == 0)
        {
            logger?.LogInformation(
                "Collection import skipped because the source collection had no importable content. DestinationCollectionId={DestinationCollectionId} SourceCollectionId={SourceCollectionId} ImportFolderTitle={ImportFolderTitle}",
                collectionId,
                sourceCollectionId,
                importTitle);
            return CreateImportResult(sourceRoot, importTitle);
        }

        ITreeRepository destinationRepository = OpenTreeRepositoryForCollectionPath(destinationRoot);
        string importFolderTitle = CreateUniqueImportFolderTitle(destinationRepository, importTitle);
        string importDirectoryRelativePath = CreateUniqueImportDirectoryRelativePath(destinationRoot, importFolderTitle);
        Directory.CreateDirectory(Path.Combine(destinationRoot, ToPlatformPath(importDirectoryRelativePath)));

        long importFolderId = destinationRepository.CreateFolder(parentId: null, importFolderTitle);
        CollectionImportState state = new(
            sourceRoot,
            destinationRoot,
            destinationRepository,
            importDirectoryRelativePath,
            logger);

        ImportCollectionAttachments(snapshot, state);

        foreach (SourceTreeItem rootItem in snapshot.RootItems)
        {
            ImportTreeItem(rootItem, importFolderId, state);
        }

        ImportNoteAttachmentLinks(snapshot, state);
        ApplyImportedTags(snapshot, state);
        RefreshImportedNoteMentionIndexes(state);

        InvalidateNoteCatalogCache(destinationRoot);

        KnowledgeCollectionImportReadModel importedCollection = CreateImportResult(
            sourceRoot,
            importFolderTitle,
            state);
        logger?.LogInformation(
            "Collection import completed. DestinationCollectionId={DestinationCollectionId} SourceCollectionId={SourceCollectionId} ImportFolderTitle={ImportFolderTitle} ImportedFolders={ImportedFolders} ImportedNotes={ImportedNotes} ImportedPdfDocuments={ImportedPdfDocuments} ImportedAttachments={ImportedAttachments} ImportedAttachmentLinks={ImportedAttachmentLinks} ImportedTaggedItems={ImportedTaggedItems} SkippedMissingFiles={SkippedMissingFiles}",
            collectionId,
            sourceCollectionId,
            importFolderTitle,
            importedCollection.ImportedFolders,
            importedCollection.ImportedNotes,
            importedCollection.ImportedPdfDocuments,
            importedCollection.ImportedAttachments,
            importedCollection.ImportedAttachmentLinks,
            importedCollection.ImportedTaggedItems,
            importedCollection.SkippedMissingFiles);
        return importedCollection;
    }

    private SourceCollectionSnapshot CaptureSourceCollectionSnapshot(string sourceRoot)
    {
        ITreeRepository sourceRepository = OpenTreeRepositoryForCollectionPath(sourceRoot);
        List<KnowledgeTreeItemRow> flatItems = [];
        SourceTreeItem[] roots = sourceRepository
            .LoadRootItems()
            .Select(row => CaptureSourceTreeItem(sourceRepository, row, flatItems))
            .ToArray();
        IReadOnlyDictionary<long, IReadOnlyList<string>> tagsByItemId = LoadSourceTagsByItemId(
            sourceRepository,
            flatItems);
        IReadOnlyList<AttachmentRow> attachments = sourceRepository.LoadAttachments();

        return new SourceCollectionSnapshot(
            roots,
            tagsByItemId,
            attachments.ToDictionary(attachment => attachment.Id, StringComparer.OrdinalIgnoreCase),
            sourceRepository.LoadNoteAttachments());
    }

    private static SourceTreeItem CaptureSourceTreeItem(
        ITreeRepository repository,
        KnowledgeTreeItemRow row,
        List<KnowledgeTreeItemRow> flatItems)
    {
        flatItems.Add(row);
        SourceTreeItem[] children = row.ChildCount <= 0
            ? []
            : repository
                .LoadChildren(row.Id)
                .Select(child => CaptureSourceTreeItem(repository, child, flatItems))
                .ToArray();

        return new SourceTreeItem(row, children);
    }

    private static IReadOnlyDictionary<long, IReadOnlyList<string>> LoadSourceTagsByItemId(
        ITreeRepository repository,
        IReadOnlyList<KnowledgeTreeItemRow> items)
    {
        long[] itemIds = items.Select(item => item.Id).ToArray();
        if (itemIds.Length == 0)
        {
            return new Dictionary<long, IReadOnlyList<string>>();
        }

        return repository
            .LoadTagsByItemIds(itemIds)
            .GroupBy(row => row.ItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.DisplayName)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
    }

    private static void EnsureImportableCollectionRoots(string destinationRoot, string sourceRoot)
    {
        if (string.Equals(destinationRoot, sourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The active collection cannot import itself.");
        }

        if (IsPathInsideRoot(sourceRoot, destinationRoot)
            || IsPathInsideRoot(destinationRoot, sourceRoot))
        {
            throw new InvalidOperationException("Nested collections cannot be imported into each other.");
        }
    }

    private static string CreateImportedCollectionTitle(string sourceRoot)
    {
        string sourceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceRoot));
        if (sourceName.EndsWith(".cognt", StringComparison.OrdinalIgnoreCase))
        {
            sourceName = sourceName[..^".cognt".Length];
        }

        return string.IsNullOrWhiteSpace(sourceName)
            ? "Imported Collection"
            : sourceName.Trim();
    }

    private static string CreateUniqueImportFolderTitle(
        ITreeRepository repository,
        string baseTitle)
    {
        HashSet<string> rootTitles = repository
            .LoadRootItems()
            .Select(item => item.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!rootTitles.Contains(baseTitle))
        {
            return baseTitle;
        }

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidate = $"{baseTitle} {suffix}";
            if (!rootTitles.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("A unique import folder name could not be created.");
    }

    private static string CreateUniqueImportDirectoryRelativePath(
        string destinationRoot,
        string importFolderTitle)
    {
        string directoryName = NormalizeFileSystemName(importFolderTitle, "Import folder name");
        string baseRelativePath = NormalizeRelativePath(Path.Combine(FilesDirectoryName, directoryName));

        for (int suffix = 1; suffix < int.MaxValue; suffix++)
        {
            string relativePath = suffix == 1
                ? baseRelativePath
                : NormalizeRelativePath(Path.Combine(FilesDirectoryName, $"{directoryName} {suffix}"));
            string fullPath = Path.GetFullPath(Path.Combine(destinationRoot, ToPlatformPath(relativePath)));
            if (IsPathInsideRoot(fullPath, destinationRoot)
                && !Directory.Exists(fullPath)
                && !File.Exists(fullPath))
            {
                return relativePath;
            }
        }

        throw new InvalidOperationException("A unique import storage directory could not be created.");
    }

    private static void ImportCollectionAttachments(
        SourceCollectionSnapshot snapshot,
        CollectionImportState state)
    {
        HashSet<string> linkedAttachmentIds = snapshot.NoteAttachments
            .Select(reference => reference.AttachmentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (AttachmentRow sourceAttachment in snapshot.AttachmentsById.Values)
        {
            KnowledgeAttachmentKind kind = InferAttachmentKind(
                sourceAttachment.FileExtension,
                sourceAttachment.ContentType);
            if (kind == KnowledgeAttachmentKind.Pdf && !linkedAttachmentIds.Contains(sourceAttachment.Id))
            {
                continue;
            }

            AttachmentRow? existingAttachment = state.Repository.FindAttachmentBySha256(sourceAttachment.Sha256);
            if (existingAttachment is not null)
            {
                state.MapAttachment(sourceAttachment, existingAttachment);
                TryRestoreExistingAttachmentFile(sourceAttachment, existingAttachment, state);
                continue;
            }

            if (!TryResolveSourceFilePath(state.SourceRoot, sourceAttachment.RelativePath, out string sourcePath))
            {
                state.SkippedMissingFiles++;
                state.Logger?.LogWarning(
                    "Skipped collection attachment import because the source file could not be resolved. AttachmentId={AttachmentId} RelativePath={RelativePath} OriginalFileName={OriginalFileName}",
                    sourceAttachment.Id,
                    sourceAttachment.RelativePath,
                    sourceAttachment.OriginalFileName);
                continue;
            }

            string destinationRelativePath = CreateAttachmentRelPath(
                sourceAttachment.Sha256,
                sourceAttachment.FileExtension,
                kind);
            string destinationPath = GetStoredAttachmentPath(state.DestinationRoot, destinationRelativePath);
            CopyFileIfMissing(sourcePath, destinationPath);

            AttachmentRow importedAttachment = new(
                CreateAttachmentId(sourceAttachment.Sha256),
                sourceAttachment.Sha256,
                sourceAttachment.OriginalFileName,
                Path.GetFileName(destinationPath),
                destinationRelativePath,
                sourceAttachment.ContentType,
                sourceAttachment.FileExtension,
                sourceAttachment.SizeBytes,
                sourceAttachment.CreatedAt,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            state.Repository.UpsertAttachment(importedAttachment);

            AttachmentRow persistedAttachment = state.Repository.FindAttachmentBySha256(sourceAttachment.Sha256)
                ?? importedAttachment;
            state.MapAttachment(sourceAttachment, persistedAttachment);
            state.ImportedAttachments++;
        }
    }

    private static void TryRestoreExistingAttachmentFile(
        AttachmentRow sourceAttachment,
        AttachmentRow destinationAttachment,
        CollectionImportState state)
    {
        if (TryResolveSourceFilePath(state.SourceRoot, sourceAttachment.RelativePath, out string sourcePath))
        {
            CopyFileIfMissing(
                sourcePath,
                GetStoredAttachmentPath(state.DestinationRoot, destinationAttachment.RelativePath));
        }
    }

    private static void ImportTreeItem(
        SourceTreeItem sourceItem,
        long parentId,
        CollectionImportState state)
    {
        KnowledgeTreeItemRow row = sourceItem.Row;
        KnowledgeTreeItemRow? importedItem = row.Kind switch
        {
            KnowledgeTreeItemKind.Folder => CreateImportedFolder(row, parentId, state),
            KnowledgeTreeItemKind.Note => CreateImportedNote(row, parentId, state),
            KnowledgeTreeItemKind.PdfDocument => CreateImportedPdfDocument(row, parentId, state),
            _ => null
        };

        long childParentId = importedItem?.Id ?? parentId;
        foreach (SourceTreeItem child in sourceItem.Children)
        {
            ImportTreeItem(child, childParentId, state);
        }
    }

    private static KnowledgeTreeItemRow CreateImportedFolder(
        KnowledgeTreeItemRow sourceRow,
        long parentId,
        CollectionImportState state)
    {
        long itemId = state.Repository.CreateFolder(parentId, sourceRow.Title);
        KnowledgeTreeItemRow imported = GetImportedTreeItem(state.Repository, itemId);
        state.MapItem(sourceRow.Id, imported);
        state.ImportedFolders++;
        return imported;
    }

    private static KnowledgeTreeItemRow? CreateImportedNote(
        KnowledgeTreeItemRow sourceRow,
        long parentId,
        CollectionImportState state)
    {
        if (string.IsNullOrWhiteSpace(sourceRow.FileRelPath)
            || !TryCopyImportedTreeFile(sourceRow, state, out string importedRelativePath, out string importedPath))
        {
            state.SkippedMissingFiles++;
            state.Logger?.LogWarning(
                "Skipped collection note import because the source note file could not be resolved. SourceItemId={SourceItemId} SourceTitle={SourceTitle} SourceRelativePath={SourceRelativePath}",
                sourceRow.Id,
                sourceRow.Title,
                sourceRow.FileRelPath);
            return null;
        }

        long itemId = state.Repository.CreateNote(parentId, sourceRow.Title, importedRelativePath);
        KnowledgeTreeItemRow imported = GetImportedTreeItem(state.Repository, itemId);
        state.MapItem(sourceRow.Id, imported);
        state.MapDocumentPath(sourceRow.FileRelPath, importedRelativePath);
        state.ImportedNotePaths.Add(importedPath);
        CopyCardContextDraftIfPresent(sourceRow.FileRelPath, importedRelativePath, state);
        state.ImportedNotes++;
        return imported;
    }

    private static void CopyCardContextDraftIfPresent(
        string? sourceRelativePath,
        string importedRelativePath,
        CollectionImportState state)
    {
        if (string.IsNullOrWhiteSpace(sourceRelativePath))
        {
            return;
        }

        string sourceSidecarPath = GetCardContextDraftSidecarPath(
            state.SourceRoot,
            NormalizeRelativePath(sourceRelativePath));
        if (File.Exists(sourceSidecarPath))
        {
            CopyFileIfMissing(
                sourceSidecarPath,
                GetCardContextDraftSidecarPath(state.DestinationRoot, NormalizeRelativePath(importedRelativePath)));
        }
    }

    private static KnowledgeTreeItemRow? CreateImportedPdfDocument(
        KnowledgeTreeItemRow sourceRow,
        long parentId,
        CollectionImportState state)
    {
        string sourceRelativePath = NormalizeRelativePath(sourceRow.FileRelPath ?? string.Empty);
        if (sourceRelativePath.Length == 0
            || !state.AttachmentRelativePathMap.TryGetValue(sourceRelativePath, out string? importedRelativePath)
            && !TryCopyImportedTreeFile(sourceRow, state, out importedRelativePath, out _))
        {
            state.SkippedMissingFiles++;
            state.Logger?.LogWarning(
                "Skipped collection PDF import because the source file could not be resolved. SourceItemId={SourceItemId} SourceTitle={SourceTitle} SourceRelativePath={SourceRelativePath}",
                sourceRow.Id,
                sourceRow.Title,
                sourceRow.FileRelPath);
            return null;
        }

        long itemId = state.Repository.CreatePdfDocument(parentId, sourceRow.Title, importedRelativePath);
        KnowledgeTreeItemRow imported = GetImportedTreeItem(state.Repository, itemId);
        state.MapItem(sourceRow.Id, imported);
        state.MapDocumentPath(sourceRow.FileRelPath, importedRelativePath);
        state.ImportedPdfDocuments++;
        return imported;
    }

    private static bool TryCopyImportedTreeFile(
        KnowledgeTreeItemRow sourceRow,
        CollectionImportState state,
        out string importedRelativePath,
        out string importedPath)
    {
        importedRelativePath = string.Empty;
        importedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceRow.FileRelPath)
            || !TryResolveSourceFilePath(state.SourceRoot, sourceRow.FileRelPath, out string sourcePath))
        {
            return false;
        }

        importedRelativePath = CreateImportedTreeFileRelativePath(sourceRow, state);
        importedPath = Path.GetFullPath(Path.Combine(state.DestinationRoot, ToPlatformPath(importedRelativePath)));
        if (!IsPathInsideRoot(importedPath, state.DestinationRoot))
        {
            throw new UnauthorizedAccessException("The imported file path is outside the active collection root.");
        }

        CopyFileIfMissing(sourcePath, importedPath);
        return true;
    }

    private static string CreateImportedTreeFileRelativePath(
        KnowledgeTreeItemRow sourceRow,
        CollectionImportState state)
    {
        string sourceRelativePath = NormalizeRelativePath(sourceRow.FileRelPath ?? string.Empty);
        string importSubPath = StripKnownCollectionFilePrefix(sourceRelativePath);
        if (string.IsNullOrWhiteSpace(importSubPath)
            || HasRelativeTraversal(importSubPath))
        {
            importSubPath = Path.GetFileName(ToPlatformPath(sourceRelativePath));
        }

        if (string.IsNullOrWhiteSpace(importSubPath))
        {
            string extension = sourceRow.Kind == KnowledgeTreeItemKind.PdfDocument
                ? PdfExtension
                : BlockSuiteNoteExtension;
            importSubPath = $"{NormalizeFileSystemName(sourceRow.Title, "Imported file name")}{extension}";
        }

        string candidate = NormalizeRelativePath(Path.Combine(
            state.ImportDirectoryRelativePath,
            ToPlatformPath(importSubPath)));
        return AllocateUniqueImportedRelativePath(candidate, state);
    }

    private static string StripKnownCollectionFilePrefix(string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        foreach (string directoryName in GetKnownLooseNoteDirectoryNames())
        {
            if (normalized.Length > directoryName.Length
                && normalized[directoryName.Length] == '/'
                && normalized.StartsWith(directoryName, StringComparison.OrdinalIgnoreCase))
            {
                return normalized[(directoryName.Length + 1)..];
            }
        }

        return normalized.StartsWith($"{AttachmentsDirectoryName}/", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(ToPlatformPath(normalized))
            : normalized;
    }

    private static bool HasRelativeTraversal(string relativePath)
        => NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));

    private static string AllocateUniqueImportedRelativePath(
        string candidateRelativePath,
        CollectionImportState state)
    {
        string normalizedCandidate = NormalizeRelativePath(candidateRelativePath);
        string directory = NormalizeRelativePath(Path.GetDirectoryName(ToPlatformPath(normalizedCandidate)) ?? string.Empty);
        string fileName = Path.GetFileNameWithoutExtension(ToPlatformPath(normalizedCandidate));
        string extension = Path.GetExtension(ToPlatformPath(normalizedCandidate));

        for (int suffix = 1; suffix < int.MaxValue; suffix++)
        {
            string suffixedFileName = suffix == 1 ? $"{fileName}{extension}" : $"{fileName} {suffix}{extension}";
            string relativePath = string.IsNullOrWhiteSpace(directory)
                ? suffixedFileName
                : NormalizeRelativePath(Path.Combine(ToPlatformPath(directory), suffixedFileName));
            string fullPath = Path.GetFullPath(Path.Combine(state.DestinationRoot, ToPlatformPath(relativePath)));

            if (IsPathInsideRoot(fullPath, state.DestinationRoot)
                && state.AllocatedRelativePaths.Add(relativePath)
                && !File.Exists(fullPath)
                && !Directory.Exists(fullPath))
            {
                return relativePath;
            }
        }

        throw new InvalidOperationException("A unique imported file path could not be created.");
    }

    private static bool TryResolveSourceFilePath(
        string sourceRoot,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string platformRelativePath = ToPlatformPath(NormalizeRelativePath(relativePath));
        if (Path.IsPathRooted(platformRelativePath))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(sourceRoot, platformRelativePath));
        if (!IsPathInsideRoot(candidate, sourceRoot) || !File.Exists(candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static void CopyFileIfMissing(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The imported file does not have a parent directory."));
        File.Copy(sourcePath, destinationPath, overwrite: false);
    }

    private static void ImportNoteAttachmentLinks(
        SourceCollectionSnapshot snapshot,
        CollectionImportState state)
    {
        foreach (NoteAttachmentRow sourceReference in snapshot.NoteAttachments)
        {
            if (!state.ImportedItemsBySourceId.TryGetValue(sourceReference.NoteItemId, out KnowledgeTreeItemRow? note)
                || !state.AttachmentIdMap.TryGetValue(sourceReference.AttachmentId, out string? attachmentId))
            {
                continue;
            }

            NoteAttachmentRow importedReference = new(
                CreateNoteAttachmentId(note.Uid, attachmentId),
                note.Id,
                note.Uid,
                attachmentId,
                sourceReference.DisplayName,
                sourceReference.CreatedAt);
            state.Repository.UpsertNoteAttachment(importedReference);
            state.ImportedAttachmentLinks++;
        }
    }

    private static void ApplyImportedTags(
        SourceCollectionSnapshot snapshot,
        CollectionImportState state)
    {
        foreach ((long sourceItemId, KnowledgeTreeItemRow importedItem) in state.ImportedItemsBySourceId)
        {
            if (!snapshot.TagsByItemId.TryGetValue(sourceItemId, out IReadOnlyList<string>? tags)
                || tags.Count == 0)
            {
                continue;
            }

            state.Repository.AssignTagsToItems([importedItem.Id], tags);
            state.ImportedTaggedItems++;
        }
    }

    private void RefreshImportedNoteMentionIndexes(CollectionImportState state)
    {
        foreach (string notePath in state.ImportedNotePaths)
        {
            string content = File.ReadAllText(notePath);
            TryRefreshSavedNoteMentionIndex(notePath, content);
        }
    }

    private static KnowledgeTreeItemRow GetImportedTreeItem(ITreeRepository repository, long itemId)
        => repository.GetItem(itemId)
            ?? throw new InvalidOperationException("The imported tree item could not be loaded.");

    private static KnowledgeCollectionImportReadModel CreateImportResult(
        string sourceRoot,
        string importFolderTitle,
        CollectionImportState? state = null)
    {
        return new(
            sourceRoot,
            importFolderTitle,
            state?.ImportedFolders ?? 0,
            state?.ImportedNotes ?? 0,
            state?.ImportedPdfDocuments ?? 0,
            state?.ImportedAttachments ?? 0,
            state?.ImportedAttachmentLinks ?? 0,
            state?.ImportedTaggedItems ?? 0,
            state?.SkippedMissingFiles ?? 0,
            state?.DocumentRelativePathMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record SourceCollectionSnapshot(
        IReadOnlyList<SourceTreeItem> RootItems,
        IReadOnlyDictionary<long, IReadOnlyList<string>> TagsByItemId,
        IReadOnlyDictionary<string, AttachmentRow> AttachmentsById,
        IReadOnlyList<NoteAttachmentRow> NoteAttachments);

    private sealed record SourceTreeItem(
        KnowledgeTreeItemRow Row,
        IReadOnlyList<SourceTreeItem> Children);

    private sealed class CollectionImportState(
        string sourceRoot,
        string destinationRoot,
        ITreeRepository repository,
        string importDirectoryRelativePath,
        ILogger<NoteService>? logger)
    {
        internal readonly string SourceRoot = sourceRoot;
        internal readonly string DestinationRoot = destinationRoot;
        internal readonly ITreeRepository Repository = repository;
        internal readonly string ImportDirectoryRelativePath = importDirectoryRelativePath;
        internal readonly ILogger<NoteService>? Logger = logger;

        internal HashSet<string> AllocatedRelativePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<long, KnowledgeTreeItemRow> ImportedItemsBySourceId { get; } = [];

        internal Dictionary<string, string> AttachmentIdMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, string> AttachmentRelativePathMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, string> DocumentRelativePathMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<string> ImportedNotePaths { get; } = [];

        internal int ImportedFolders;
        internal int ImportedNotes;
        internal int ImportedPdfDocuments;
        internal int ImportedAttachments;
        internal int ImportedAttachmentLinks;
        internal int ImportedTaggedItems;
        internal int SkippedMissingFiles;

        internal void MapItem(long sourceItemId, KnowledgeTreeItemRow importedItem)
        {
            ImportedItemsBySourceId[sourceItemId] = importedItem;
        }

        internal void MapAttachment(AttachmentRow sourceAttachment, AttachmentRow importedAttachment)
        {
            AttachmentIdMap[sourceAttachment.Id] = importedAttachment.Id;
            AttachmentRelativePathMap[NormalizeRelativePath(sourceAttachment.RelativePath)] =
                NormalizeRelativePath(importedAttachment.RelativePath);
        }

        internal void MapDocumentPath(string? sourceRelativePath, string importedRelativePath)
        {
            if (!string.IsNullOrWhiteSpace(sourceRelativePath)
                && !string.IsNullOrWhiteSpace(importedRelativePath))
            {
                DocumentRelativePathMap[NormalizeRelativePath(sourceRelativePath)] = NormalizeRelativePath(importedRelativePath);
            }
        }
    }
}
