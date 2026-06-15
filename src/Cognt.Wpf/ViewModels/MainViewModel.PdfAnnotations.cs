using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.Views;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal async Task<string> CreatePdfAnnotationsNoteAsync(PdfAnnotationExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before exporting PDF annotations.");
        }

        PdfStudyAnnotation[] annotations = export.Annotations
            .Where(annotation => annotation.PageNumber > 0)
            .OrderBy(annotation => annotation.PageNumber)
            .ThenBy(annotation => annotation.CreatedAtUtc)
            .ToArray();
        if (annotations.Length == 0)
        {
            throw new InvalidOperationException("There are no PDF annotations to export.");
        }

        SaveCurrentNoteFromEditor();
        string title = CreatePdfAnnotationNoteTitle(export.SourceTitle);
        KnowledgeCreatedNoteReadModel createdNote = await Task.Run(() =>
        {
            string content = documentContentFactory.FromMarkdown(
                CreatePdfAnnotationMarkdown(export, annotations),
                title);
            return noteService.CreateNote(openCollectionPath, parentId: null, title, content);
        });

        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
        return createdNote.RelativePath;
    }

    private static string CreatePdfAnnotationNoteTitle(string sourceTitle)
    {
        string title = string.IsNullOrWhiteSpace(sourceTitle) ? "PDF" : sourceTitle.Trim();
        const int maximumSourceTitleLength = 100;
        if (title.Length > maximumSourceTitleLength)
        {
            title = string.Concat(title.AsSpan(0, maximumSourceTitleLength - 3), "...");
        }

        return string.Concat(title, " annotations");
    }

    private static string CreatePdfAnnotationMarkdown(
        PdfAnnotationExport export,
        IReadOnlyList<PdfStudyAnnotation> annotations)
    {
        StringBuilder markdown = new();
        markdown.Append("Source PDF: `")
            .Append(Path.GetFileName(export.SourcePath).Replace("`", "'", StringComparison.Ordinal))
            .AppendLine("`");
        markdown.Append("Exported: ")
            .AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        markdown.AppendLine();

        foreach (IGrouping<int, PdfStudyAnnotation> page in annotations.GroupBy(annotation => annotation.PageNumber))
        {
            markdown.Append("## Page ").AppendLine(page.Key.ToString());
            markdown.AppendLine();
            foreach (PdfStudyAnnotation annotation in page)
            {
                markdown.Append("### ")
                    .AppendLine(annotation.Kind == PdfStudyAnnotationKind.Comment ? "Comment" : "Highlight");
                if (!string.IsNullOrWhiteSpace(annotation.SelectedText))
                {
                    foreach (string line in annotation.SelectedText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                    {
                        markdown.Append("> ").AppendLine(line.Trim());
                    }

                    markdown.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(annotation.Comment))
                {
                    markdown.AppendLine(annotation.Comment.Trim());
                    markdown.AppendLine();
                }
            }
        }

        return markdown.ToString();
    }
}
