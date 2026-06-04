using System.Globalization;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class GraphViewModel
{
    private void LoadSettings()
    {
        if (!mainViewModel.HasOpenCollection)
        {
            ApplySettings(KnowledgeGraphSettingsReadModel.Default);
            return;
        }

        KnowledgeGraphSettingsReadModel settings = noteService.LoadGraphViewSettings(mainViewModel.OpenCollectionPath);
        ApplySettings(settings);
    }

    private bool TryCreateSettings(out KnowledgeGraphSettingsReadModel settings)
    {
        if (!int.TryParse(NeighborDepthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int neighborDepthValue))
        {
            settings = KnowledgeGraphSettingsReadModel.Default;
            ErrorMessage = "Depth must be a whole number from 1 to 4.";
            return false;
        }

        if (!int.TryParse(MaxNodesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxNodes))
        {
            settings = KnowledgeGraphSettingsReadModel.Default;
            ErrorMessage = "Max nodes must be a whole number.";
            return false;
        }

        settings = new KnowledgeGraphSettingsReadModel(neighborDepthValue, maxNodes).Normalize();
        NeighborDepth = settings.NeighborDepth;
        MaxNodesText = settings.MaxNodes.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private void ApplySettings(KnowledgeGraphSettingsReadModel settings)
    {
        KnowledgeGraphSettingsReadModel normalized = settings.Normalize();
        NeighborDepth = normalized.NeighborDepth;
        MaxNodesText = normalized.MaxNodes.ToString(CultureInfo.InvariantCulture);
    }
}
