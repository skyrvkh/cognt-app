using System.Text.RegularExpressions;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class GraphViewModel
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    internal string CreatePreviewText(KnowledgeGraphNodeReadModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        try
        {
            string preview = mainViewModel.ReadMentionPreview(node.RelativePath, 260);
            preview = WhitespaceRegex.Replace(preview ?? string.Empty, " ").Trim();
            if (preview.Length > 260)
            {
                preview = $"{preview[..257]}...";
            }

            return string.IsNullOrWhiteSpace(preview)
                ? node.Title
                : $"{node.Title}{Environment.NewLine}{preview}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Graph node preview failed; using fallback title. DocumentId={DocumentId} RelativePath={RelativePath} Title={Title}",
                node.DocumentId,
                node.RelativePath,
                node.Title);
            return string.IsNullOrWhiteSpace(node.Title)
                ? node.RelativePath
                : node.Title;
        }
    }
}
