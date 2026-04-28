using System.Text.Json.Nodes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string GetStableBlockSuiteBlockId(JsonObject block)
    {
        return GetTrimmedJsonStringProperty(block, "id")
            ?? GetTrimmedJsonStringProperty(block["props"] as JsonObject, "id")
            ?? CreateShortHash(block.ToJsonString());
    }

    private static JsonObject? GetBlockSuiteSnapshotBlocksRoot(JsonNode? rootNode)
    {
        if (rootNode is not JsonObject rootObject)
        {
            return null;
        }

        JsonNode? snapshot = rootObject["snapshot"] ?? rootObject;
        return snapshot is JsonObject snapshotObject
            && snapshotObject["blocks"] is JsonObject blocks
                ? blocks
                : null;
    }
}
