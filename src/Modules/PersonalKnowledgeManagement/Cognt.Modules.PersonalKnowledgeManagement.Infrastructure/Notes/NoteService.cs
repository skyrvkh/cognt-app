using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService : IDisposable
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
    private static readonly HashSet<string> DocumentAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        PdfExtension,
        DocExtension,
        DocxExtension,
        RtfExtension,
        TextExtension,
        MarkdownNoteExtension,
        MarkdownImportExtension
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
    private bool disposed;

    private static string GetExistingCollectionRoot(string collectionId)
    {
        return ResolveExistingCollectionRoot(collectionId);
    }

    internal string ReadNoteFile(string collectionId, string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string fullPath = ResolveExistingRelativeNotePathInRoot(root, relativePath);
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    internal KnowledgeOpenedNoteReadModel ReadNoteForOpen(string collectionId, string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
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

    internal void CloseCurrentCollection()
    {
        lock (repositoryLock)
        {
            ThrowIfDisposed();
            CloseTreeRepository();
        }
    }
}

