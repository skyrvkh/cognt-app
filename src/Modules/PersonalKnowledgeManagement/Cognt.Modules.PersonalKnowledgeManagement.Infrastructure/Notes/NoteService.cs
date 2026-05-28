using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService : INoteService
{
    private const string MetadataDatabaseFileName = "cognt.sqlite";
    private const string FilesDirectoryName = "collection";
    private const string LegacyFilesDirectoryName = "Files";
    private const string AttachmentsDirectoryName = "attachments";
    private const string ImagesDirectoryName = "images";
    private const string FlashcardsDirectoryName = "flashcards";
    private const string LegacyFlashcardsDirectoryName = "Flashcards";
    private const string BlockSuiteNoteExtension = ".json";
    private const string MarkdownNoteExtension = ".md";
    private const string MarkdownImportExtension = ".markdown";
    private const string PdfExtension = ".pdf";
    private const string DocExtension = ".doc";
    private const string DocxExtension = ".docx";
    private const string RtfExtension = ".rtf";
    private const string TextExtension = ".txt";
    private const string PngExtension = ".png";
    private const string JpgExtension = ".jpg";
    private const string JpegExtension = ".jpeg";
    private const string WebpExtension = ".webp";
    private const string GifExtension = ".gif";
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions BlockSuiteJsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly HashSet<string> DocumentAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        PdfExtension,
        DocExtension,
        DocxExtension,
        MarkdownNoteExtension,
        MarkdownImportExtension,
        RtfExtension,
        TextExtension
    };
    private static readonly HashSet<string> ImageAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        PngExtension,
        JpgExtension,
        JpegExtension,
        WebpExtension,
        GifExtension
    };

    private readonly object repositoryLock = new();
    private ITreeRepository? treeRepository;
    private string? treeRepositoryRoot;
    private string? cachedNoteCatalogRoot;
    private IReadOnlyList<NoteCatalogEntry>? cachedNoteCatalogEntries;
    private bool disposed;
    private readonly ILogger<NoteService>? logger;

    internal NoteService(ILogger<NoteService>? logger = null)
    {
        this.logger = logger;
    }

    private string GetExistingCollectionRoot(string collectionId)
    {
        return ResolveExistingCollectionRoot(collectionId);
    }

    string INoteService.ReadNoteFile(string collectionId, string relativePath)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);

        string root = GetExistingCollectionRoot(collectionId);
        string fullPath = ResolveExistingRelativeNotePathInRoot(root, normalizedRelativePath);
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    KnowledgeOpenedNoteReadModel INoteService.ReadNoteForOpen(string collectionId, string relativePath)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);

        string root = GetExistingCollectionRoot(collectionId);
        string fullPath = ResolveExistingRelativeNotePathInRoot(root, normalizedRelativePath);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        KnowledgeTreeItemRow? note = repository.FindNoteByFileRelPath(normalizedRelativePath);
        string content = File.ReadAllText(fullPath, Encoding.UTF8);
        return new KnowledgeOpenedNoteReadModel(
            normalizedRelativePath,
            note?.Title ?? Path.GetFileNameWithoutExtension(fullPath),
            content,
            note?.Id);
    }

    void INoteService.CloseCurrentCollection()
    {
        lock (repositoryLock)
        {
            ThrowIfDisposed();
            InvalidateNoteCatalogCache();
            CloseTreeRepository();
        }
    }
}

