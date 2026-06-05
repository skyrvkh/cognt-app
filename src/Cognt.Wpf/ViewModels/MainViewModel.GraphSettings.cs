using System.Globalization;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private KnowledgeGraphSettingsReadModel persistedGraphSettings = KnowledgeGraphSettingsReadModel.Default;
    private bool isGraphSettingsLoading;
    private bool isGraphSettingsSaving;
    private int graphSettingsNeighborDepth = KnowledgeGraphSettingsReadModel.DefaultNeighborDepth;
    private string graphSettingsNeighborDepthText = KnowledgeGraphSettingsReadModel.DefaultNeighborDepth.ToString(CultureInfo.InvariantCulture);
    private string graphSettingsMaxNodesText = KnowledgeGraphSettingsReadModel.DefaultMaxNodes.ToString(CultureInfo.InvariantCulture);
    private string graphSettingsStatusMessage = string.Empty;
    private string graphSettingsValidationMessage = string.Empty;
    private string graphSettingsErrorMessage = string.Empty;

    public int GraphSettingsNeighborDepth
    {
        get => graphSettingsNeighborDepth;
        set
        {
            int normalized = Math.Clamp(
                value,
                KnowledgeGraphSettingsReadModel.MinimumNeighborDepth,
                KnowledgeGraphSettingsReadModel.MaximumNeighborDepth);
            if (SetProperty(ref graphSettingsNeighborDepth, normalized))
            {
                graphSettingsNeighborDepthText = normalized.ToString(CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(GraphSettingsNeighborDepthText));
                ClearGraphSettingsFeedback(clearValidation: false);
                OnPropertyChanged(nameof(GraphSettingsNeighborDepthDisplay));
                UpdateGraphSettingsValidation();
            }
        }
    }

    public string GraphSettingsNeighborDepthDisplay =>
        GraphSettingsNeighborDepth == 1
            ? "1 hop"
            : $"{GraphSettingsNeighborDepth} hops";

    public string GraphSettingsNeighborDepthText
    {
        get => graphSettingsNeighborDepthText;
        set
        {
            if (!SetProperty(ref graphSettingsNeighborDepthText, value))
            {
                return;
            }

            ClearGraphSettingsFeedback(clearValidation: false);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                int normalized = Math.Clamp(
                    parsed,
                    KnowledgeGraphSettingsReadModel.MinimumNeighborDepth,
                    KnowledgeGraphSettingsReadModel.MaximumNeighborDepth);
                if (graphSettingsNeighborDepth != normalized)
                {
                    graphSettingsNeighborDepth = normalized;
                    OnPropertyChanged(nameof(GraphSettingsNeighborDepth));
                    OnPropertyChanged(nameof(GraphSettingsNeighborDepthDisplay));
                }
            }

            UpdateGraphSettingsValidation();
        }
    }

    public string GraphSettingsMaxNodesText
    {
        get => graphSettingsMaxNodesText;
        set
        {
            if (SetProperty(ref graphSettingsMaxNodesText, value))
            {
                ClearGraphSettingsFeedback(clearValidation: false);
                UpdateGraphSettingsValidation();
            }
        }
    }

    public string GraphSettingsStatusMessage
    {
        get => graphSettingsStatusMessage;
        private set => SetProperty(ref graphSettingsStatusMessage, value);
    }

    public string GraphSettingsValidationMessage
    {
        get => graphSettingsValidationMessage;
        private set
        {
            if (SetProperty(ref graphSettingsValidationMessage, value))
            {
                OnPropertyChanged(nameof(HasGraphSettingsValidationMessage));
                OnPropertyChanged(nameof(CanSaveGraphSettings));
                RaiseSettingsCommandStates();
            }
        }
    }

    public bool HasGraphSettingsValidationMessage => !string.IsNullOrWhiteSpace(graphSettingsValidationMessage);

    public string GraphSettingsErrorMessage
    {
        get => graphSettingsErrorMessage;
        private set
        {
            if (SetProperty(ref graphSettingsErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasGraphSettingsErrorMessage));
            }
        }
    }

    public bool HasGraphSettingsErrorMessage => !string.IsNullOrWhiteSpace(graphSettingsErrorMessage);

    public bool IsGraphSettingsLoading
    {
        get => isGraphSettingsLoading;
        private set
        {
            if (SetProperty(ref isGraphSettingsLoading, value))
            {
                OnPropertyChanged(nameof(CanEditGraphSettings));
                OnPropertyChanged(nameof(CanSaveGraphSettings));
                RaiseSettingsCommandStates();
            }
        }
    }

    public bool IsGraphSettingsSaving
    {
        get => isGraphSettingsSaving;
        private set
        {
            if (SetProperty(ref isGraphSettingsSaving, value))
            {
                OnPropertyChanged(nameof(CanEditGraphSettings));
                OnPropertyChanged(nameof(CanSaveGraphSettings));
                RaiseSettingsCommandStates();
            }
        }
    }

    public bool CanEditGraphSettings => HasOpenCollection && !IsGraphSettingsLoading && !IsGraphSettingsSaving;

    public bool CanSaveGraphSettings =>
        CanEditGraphSettings
        && !HasGraphSettingsValidationMessage;

    private void SaveGraphSettings()
    {
        if (!HasOpenCollection)
        {

            GraphSettingsErrorMessage = "Select a collection to edit its graph settings.";
            GraphSettingsStatusMessage = string.Empty;
            return;
        }

        ClearGraphSettingsFeedback(clearValidation: false);
        if (!TryCreateValidatedGraphSettings(out KnowledgeGraphSettingsReadModel normalized))
        {
            GraphSettingsStatusMessage = string.Empty;
            return;
        }

        try
        {
            IsGraphSettingsSaving = true;
            noteService.SaveGraphViewSettings(openCollectionPath, normalized);
            persistedGraphSettings = normalized;
            ApplyGraphSettings(normalized);
            GraphSettingsStatusMessage = $"Saved graph settings for {CurrentCollectionName}.";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Save graph settings failed. CollectionPath={CollectionPath}", openCollectionPath);
            GraphSettingsErrorMessage = $"Graph settings could not be saved: {ex.Message}";
            GraphSettingsStatusMessage = string.Empty;
        }
        finally
        {
            IsGraphSettingsSaving = false;
        }
    }

    private void RefreshGraphSettings()
    {
        if (!HasOpenCollection)
        {
            persistedGraphSettings = KnowledgeGraphSettingsReadModel.Default;
            ApplyGraphSettings(persistedGraphSettings);
            ClearGraphSettingsFeedback();
            return;
        }

        try
        {
            IsGraphSettingsLoading = true;
            KnowledgeGraphSettingsReadModel settings = noteService.LoadGraphViewSettings(openCollectionPath).Normalize();
            persistedGraphSettings = settings;
            ApplyGraphSettings(settings);
            ClearGraphSettingsFeedback();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Refresh graph settings failed. CollectionPath={CollectionPath}", openCollectionPath);
            persistedGraphSettings = KnowledgeGraphSettingsReadModel.Default;
            ApplyGraphSettings(persistedGraphSettings);
            GraphSettingsValidationMessage = string.Empty;
            GraphSettingsStatusMessage = string.Empty;
            GraphSettingsErrorMessage = $"Graph settings could not be loaded: {ex.Message}";
        }
        finally
        {
            IsGraphSettingsLoading = false;
        }
    }

    private void ApplyGraphSettings(KnowledgeGraphSettingsReadModel settings)
    {
        KnowledgeGraphSettingsReadModel normalized = settings.Normalize();
        GraphSettingsNeighborDepth = normalized.NeighborDepth;
        GraphSettingsMaxNodesText = normalized.MaxNodes.ToString(CultureInfo.InvariantCulture);
        UpdateGraphSettingsValidation();
    }

    private void ResetGraphSettings()
    {
        ApplyGraphSettings(persistedGraphSettings);
        GraphSettingsStatusMessage = "Reset graph settings to the current collection values.";
        GraphSettingsErrorMessage = string.Empty;
    }

    private bool TryCreateValidatedGraphSettings(out KnowledgeGraphSettingsReadModel settings)
    {
        GraphSettingsDraft draft = new(GraphSettingsNeighborDepthText, GraphSettingsMaxNodesText);
        if (!GraphSettingsFactory.TryCreate(draft, out settings, out string validationMessage))
        {
            GraphSettingsValidationMessage = validationMessage;
            return false;
        }

        GraphSettingsValidationMessage = string.Empty;
        return true;
    }

    private void UpdateGraphSettingsValidation()
    {
        if (!HasOpenCollection)
        {
            GraphSettingsValidationMessage = string.Empty;
            return;
        }

        TryCreateValidatedGraphSettings(out _);
    }

    private void ClearGraphSettingsFeedback(bool clearValidation = true)
    {
        GraphSettingsStatusMessage = string.Empty;
        GraphSettingsErrorMessage = string.Empty;
        if (clearValidation)
        {
            GraphSettingsValidationMessage = string.Empty;
        }
    }
}
