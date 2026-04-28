using System.Text.Json.Nodes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void AppendBlockSuiteRichTextMentions(JsonNode? textValue, DocumentMentionBuilder builder)
    {
        if (textValue is not JsonObject textObject || textObject["delta"] is not JsonArray delta)
        {
            return;
        }

        foreach (JsonNode? node in delta)
        {
            if (node is not JsonObject entry)
            {
                continue;
            }

            string? referenceTarget = null;
            string? referenceTitle = null;
            if (entry["attributes"] is JsonObject attributes
                && attributes["reference"] is JsonObject reference)
            {
                referenceTarget = GetLinkedNoteRelativePathFromReference(reference);
                referenceTitle = GetTrimmedJsonStringProperty(reference, "title");
            }

            string? insertedText = entry["insert"]?.GetValue<string>();
            builder.AppendReferenceOrText(
                referenceTarget,
                referenceTitle,
                ExplicitLinkMentionType,
                insertedText);
        }
    }

    private static string? GetLinkedNoteRelativePathFromReference(JsonObject? value)
    {
        if (value is null)
        {
            return null;
        }

        return GetTrimmedJsonStringProperty(value, "cogntLinkedNotePath")
            ?? GetTrimmedJsonStringProperty(value["params"] as JsonObject, "cogntLinkedNotePath")
            ?? GetTrimmedJsonStringProperty(value["params"] as JsonObject, "relativePath")
            ?? GetLegacyLinkedNoteRelativePath(GetTrimmedJsonStringProperty(value, "relativePath"))
            ?? GetLegacyLinkedNoteRelativePath(GetTrimmedJsonStringProperty(value, "description"));
    }

    private static string? GetTrimmedJsonStringProperty(JsonObject? value, string propertyName)
    {
        return value is not null
            && value[propertyName] is JsonValue jsonValue
            && jsonValue.TryGetValue(out string? text)
            && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;
    }

    private static string? GetLegacyLinkedNoteRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return IsNoteRelativePath(value) ? value : null;
    }
}
