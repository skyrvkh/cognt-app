using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public enum NativeDocumentImportFormat
{
    Markdown,
    Docx
}

public sealed partial class MainViewModel
{
    public async Task<KnowledgeCreatedNoteReadModel> ImportNativeDocumentAsync(
        string sourcePath,
        NativeDocumentImportFormat format,
        bool openAfterImport,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before importing documents.");
        }

        SaveCurrentNoteFromEditor();

        string title = Path.GetFileNameWithoutExtension(sourcePath).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Untitled";
        }

        string content = format switch
        {
            NativeDocumentImportFormat.Markdown => documentContentFactory.FromMarkdown(
                await File.ReadAllTextAsync(sourcePath, cancellationToken),
                title),
            NativeDocumentImportFormat.Docx => await CreateDocxContentAsync(sourcePath, title, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported native document import format.")
        };

        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            parentId: null,
            title,
            content);

        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (openAfterImport)
        {
            OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
        }

        return createdNote;
    }

    private async Task<string> CreateDocxContentAsync(
        string sourcePath,
        string title,
        CancellationToken cancellationToken)
    {
        await using FileStream sourceContent = File.OpenRead(sourcePath);
        using MemoryStream buffer = new();
        await sourceContent.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return documentContentFactory.FromDocx(buffer, title);
    }
}
