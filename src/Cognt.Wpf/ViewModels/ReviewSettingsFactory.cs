using System.Globalization;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

internal readonly record struct ReviewSettingsDraft(
    string DailyLimitText,
    string NewCardsLimitText,
    string DesiredRetentionText,
    string LearningStepsText,
    string RelearningStepsText,
    bool IsFsrsOptimizationEnabled,
    IReadOnlyList<FsrsParameterSettingViewModel> FsrsParameters,
    bool IsCramEnabled,
    bool ShowFlashcardsInNavigationTree);

internal static partial class ReviewSettingsFactory
{
    public static bool TryCreate(
        ReviewSettingsDraft draft,
        out FlashcardReviewSettingsReadModel settings,
        out string validationMessage)
    {
        List<string> errors = [];

        if (!int.TryParse(draft.DailyLimitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int reviewLimit)
            || reviewLimit < 0)
        {
            errors.Add("Daily review limit must be greater than or equal to 0.");
        }

        if (!int.TryParse(draft.NewCardsLimitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int newCardsLimit)
            || newCardsLimit < 0)
        {
            errors.Add("New card limit must be greater than or equal to 0.");
        }

        if (!TryParseRetention(draft.DesiredRetentionText, out double desiredRetention)
            || desiredRetention < FlashcardReviewSettingsReadModel.MinimumDesiredRetention
            || desiredRetention > FlashcardReviewSettingsReadModel.MaximumDesiredRetention)
        {
            errors.Add("Desired retention must be between 70% and 99%.");
        }

        if (!TryParseSteps(draft.LearningStepsText, out TimeSpan[] learningSteps))
        {
            errors.Add("Learning steps must use positive intervals such as 1m, 10m, 1h, or 1d.");
        }

        if (!TryParseSteps(draft.RelearningStepsText, out TimeSpan[] relearningSteps))
        {
            errors.Add("Relearning steps must use positive intervals such as 10m, 1h, or 1d.");
        }

        if (!TryParseFsrsParameters(draft.FsrsParameters, out double[]? fsrsParameters, out string parameterError))
        {
            errors.Add(parameterError);
        }

        if (errors.Count > 0)
        {
            settings = FlashcardReviewSettingsReadModel.Default;
            validationMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        settings = new FlashcardReviewSettingsReadModel(
            reviewLimit,
            newCardsLimit,
            desiredRetention,
            learningSteps,
            relearningSteps,
            draft.IsFsrsOptimizationEnabled,
            fsrsParameters,
            draft.IsCramEnabled,
            draft.ShowFlashcardsInNavigationTree).Normalize();
        validationMessage = string.Empty;
        return true;
    }

}
