using System.Globalization;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal readonly record struct GraphSettingsDraft(
    string NeighborDepthText,
    string MaxNodesText);

internal static class GraphSettingsFactory
{
    public static bool TryCreate(
        GraphSettingsDraft draft,
        out KnowledgeGraphSettingsReadModel settings,
        out string validationMessage)
    {
        List<string> errors = [];

        if (!int.TryParse(draft.NeighborDepthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int neighborDepth)
            || neighborDepth < KnowledgeGraphSettingsReadModel.MinimumNeighborDepth
            || neighborDepth > KnowledgeGraphSettingsReadModel.MaximumNeighborDepth)
        {
            errors.Add("Graph depth must be between 1 and 4 hops.");
        }

        if (!int.TryParse(draft.MaxNodesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxNodes)
            || maxNodes < KnowledgeGraphSettingsReadModel.MinimumMaxNodes
            || maxNodes > KnowledgeGraphSettingsReadModel.MaximumMaxNodes)
        {
            errors.Add("Graph max nodes must be between 10 and 300.");
        }

        if (errors.Count > 0)
        {
            settings = KnowledgeGraphSettingsReadModel.Default;
            validationMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        settings = new KnowledgeGraphSettingsReadModel(neighborDepth, maxNodes).Normalize();
        validationMessage = string.Empty;
        return true;
    }
}
