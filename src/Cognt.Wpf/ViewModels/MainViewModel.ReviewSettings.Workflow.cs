using System.Globalization;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Extensions.Logging;

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
                    normalized.FsrsParameters,
                    normalized.IsCramEnabled,
                    normalized.ShowFlashcardsInNavigationTree)).GetAwaiter().GetResult();

            persistedReviewSettings = normalized;
            ApplyReviewSettings(normalized);
            ReviewSettingsStatusMessage = $"Saved for {CurrentCollectionName}.";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Save review settings failed. CollectionPath={CollectionPath}", openCollectionPath);
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
            logger?.LogError(ex, "Refresh review settings failed. CollectionPath={CollectionPath}", openCollectionPath);
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
        bool previousShowFlashcardsInNavigationTree = ShowFlashcardsInNavigationTree;
        ReviewSettingsDailyLimitText = normalized.MaximumReviewCardsPerDay.ToString(CultureInfo.InvariantCulture);
        ReviewSettingsNewCardsLimitText = normalized.MaximumNewCardsPerDay.ToString(CultureInfo.InvariantCulture);
        ReviewSettingsDesiredRetention = normalized.DesiredRetention;
        ReviewSettingsLearningStepsText = ReviewSettingsFactory.FormatSteps(normalized.LearningSteps);
        ReviewSettingsRelearningStepsText = ReviewSettingsFactory.FormatSteps(normalized.RelearningSteps);
        IsFsrsOptimizationEnabled = normalized.IsOptimizationEnabled;
        IsCramEnabled = normalized.IsCramEnabled;
        ShowFlashcardsInNavigationTree = normalized.ShowFlashcardsInNavigationTree;
        ApplyFsrsParameterSettings(normalized.FsrsParameters);
        UpdateReviewSettingsValidation();

        if (HasOpenCollection
            && IsReviewSettingsLoading
            && previousShowFlashcardsInNavigationTree != ShowFlashcardsInNavigationTree)
        {
            RefreshNotesTree(preserveExpansion: true);
        }
    }

    private void ResetReviewSettings()
    {
        ApplyReviewSettings(persistedReviewSettings);
        ReviewSettingsStatusMessage = "Reset to the current collection values.";
        ReviewSettingsErrorMessage = string.Empty;
    }
}
