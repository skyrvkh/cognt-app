using System.Globalization;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void SaveReviewSettings()
    {
        if (!HasOpenCollection)
        {
            ReviewSettingsErrorMessage = "Select a collection to edit its settings.";
            ReviewSettingsStatusMessage = string.Empty;
            return;
        }

        ClearReviewSettingsFeedback(clearValidation: false);
        if (!TryCreateValidatedReviewSettings(out FlashcardReviewSettingsReadModel normalized))
        {
            ReviewSettingsStatusMessage = string.Empty;
            return;
        }

        try
        {
            IsReviewSettingsSaving = true;
            updateFlashcardReviewSettingsCommand.ExecuteAsync(
                new UpdateFlashcardReviewSettingsRequest(
                    openCollectionPath,
                    normalized.MaximumReviewCardsPerDay,
                    normalized.MaximumNewCardsPerDay,
                    normalized.DesiredRetention,
                    normalized.LearningSteps,
                    normalized.RelearningSteps,
                    normalized.IsOptimizationEnabled,
                    normalized.FsrsParameters)).GetAwaiter().GetResult();

            persistedReviewSettings = normalized;
            ApplyReviewSettings(normalized);
            ReviewSettingsStatusMessage = $"Saved for {CurrentCollectionName}.";
        }
        catch (Exception ex)
        {
            ReviewSettingsErrorMessage = $"Settings could not be saved: {ex.Message}";
            ReviewSettingsStatusMessage = string.Empty;
        }
        finally
        {
            IsReviewSettingsSaving = false;
        }
    }

    private void RefreshReviewSettings()
    {
        if (!HasOpenCollection)
        {
            persistedReviewSettings = FlashcardReviewSettingsReadModel.Default;
            ApplyReviewSettings(persistedReviewSettings);
            ClearReviewSettingsFeedback();
            return;
        }

        try
        {
            IsReviewSettingsLoading = true;
            FlashcardReviewSettingsReadModel settings = flashcardReviewSettingsQuery.Execute(
                new FlashcardCollectionQueryRequest(openCollectionPath)).Normalize();
            persistedReviewSettings = settings;
            ApplyReviewSettings(settings);
            ClearReviewSettingsFeedback();
        }
        catch (Exception ex)
        {
            persistedReviewSettings = FlashcardReviewSettingsReadModel.Default;
            ApplyReviewSettings(persistedReviewSettings);
            ReviewSettingsValidationMessage = string.Empty;
            ReviewSettingsStatusMessage = string.Empty;
            ReviewSettingsErrorMessage = $"Settings could not be loaded: {ex.Message}";
        }
        finally
        {
            IsReviewSettingsLoading = false;
        }
    }

    private void ApplyReviewSettings(FlashcardReviewSettingsReadModel settings)
    {
        FlashcardReviewSettingsReadModel normalized = settings.Normalize();
        ReviewSettingsDailyLimitText = normalized.MaximumReviewCardsPerDay.ToString(CultureInfo.InvariantCulture);
        ReviewSettingsNewCardsLimitText = normalized.MaximumNewCardsPerDay.ToString(CultureInfo.InvariantCulture);
        ReviewSettingsDesiredRetention = normalized.DesiredRetention;
        ReviewSettingsLearningStepsText = FormatReviewSteps(normalized.LearningSteps);
        ReviewSettingsRelearningStepsText = FormatReviewSteps(normalized.RelearningSteps);
        IsFsrsOptimizationEnabled = normalized.IsOptimizationEnabled;
        ApplyFsrsParameterSettings(normalized.FsrsParameters);
        UpdateReviewSettingsValidation();
    }

    private void ResetReviewSettings()
    {
        ApplyReviewSettings(persistedReviewSettings);
        ReviewSettingsStatusMessage = "Reset to the current collection values.";
        ReviewSettingsErrorMessage = string.Empty;
    }
}
