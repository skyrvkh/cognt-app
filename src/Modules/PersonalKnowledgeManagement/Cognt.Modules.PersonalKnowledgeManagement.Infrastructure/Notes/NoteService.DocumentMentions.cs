using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private const string DocumentMentionBlockId = "document";
    private const string ExplicitLinkMentionType = "explicit_link";
    private const string DocumentTitleMentionType = "document_title_mention";
    private const string ManualReferenceMentionType = "manual_reference";
    private const string ResolvedMentionStatus = "resolved";
    private const string UnresolvedMentionStatus = "unresolved";
    private const string AmbiguousMentionStatus = "ambiguous";

    private static readonly Regex RawNoteMentionRegex = new(@"!?\[\[([^\]\[\r\n]+)\]\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkRegex = new(@"\[[^\]\r\n]+\]\(([^)\r\n]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
