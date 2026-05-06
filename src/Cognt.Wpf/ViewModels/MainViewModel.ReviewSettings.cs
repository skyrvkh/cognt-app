using System.Globalization;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private FlashcardReviewSettingsReadModel persistedReviewSettings = FlashcardReviewSettingsReadModel.Default;
    private bool isReviewSettingsLoading;
    private bool isReviewSettingsSaving;
    private string reviewSettingsDailyLimitText = FlashcardReviewSettingsReadModel.DefaultMaximumReviewCardsPerDay.ToString(CultureInfo.InvariantCulture);
    private string reviewSettingsNewCardsLimitText = FlashcardReviewSettingsReadModel.DefaultMaximumNewCardsPerDay.ToString(CultureInfo.InvariantCulture);
    private double reviewSettingsDesiredRetention = FlashcardReviewSettingsReadModel.DefaultDesiredRetention;
    private string reviewSettingsStatusMessage = string.Empty;
    private string reviewSettingsValidationMessage = string.Empty;
    private string reviewSettingsErrorMessage = string.Empty;

    public string ReviewSettingsDailyLimitText
    {
        get => reviewSettingsDailyLimitText;
        set
        {
            if (SetProperty(ref reviewSettingsDailyLimitText, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public string ReviewSettingsNewCardsLimitText
    {
        get => reviewSettingsNewCardsLimitText;
        set
        {
            if (SetProperty(ref reviewSettingsNewCardsLimitText, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public double ReviewSettingsDesiredRetention
    {
        get => reviewSettingsDesiredRetention;
        set
        {
            double normalized = Math.Clamp(
                value,
                FlashcardReviewSettingsReadModel.MinimumDesiredRetention,
                FlashcardReviewSettingsReadModel.MaximumDesiredRetention);
            if (SetProperty(ref reviewSettingsDesiredRetention, normalized))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                OnPropertyChanged(nameof(ReviewSettingsDesiredRetentionDisplay));
                UpdateReviewSettingsValidation();
            }
        }
    }

    public string ReviewSettingsDesiredRetentionDisplay =>
        $"{ReviewSettingsDesiredRetention:P0}";

    public string ReviewSettingsStatusMessage
    {
        get => reviewSettingsStatusMessage;
        private set => SetProperty(ref reviewSettingsStatusMessage, value);
    }

    public string ReviewSettingsValidationMessage
    {
        get => reviewSettingsValidationMessage;
        private set
        {
            if (SetProperty(ref reviewSettingsValidationMessage, value))
            {
                OnPropertyChanged(nameof(HasReviewSettingsValidationMessage));
                OnPropertyChanged(nameof(CanSaveReviewSettings));
            }
        }
    }

    public bool HasReviewSettingsValidationMessage => !string.IsNullOrWhiteSpace(reviewSettingsValidationMessage);

    public string ReviewSettingsErrorMessage
    {
        get => reviewSettingsErrorMessage;
        private set
        {
            if (SetProperty(ref reviewSettingsErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasReviewSettingsErrorMessage));
            }
        }
    }

    public bool HasReviewSettingsErrorMessage => !string.IsNullOrWhiteSpace(reviewSettingsErrorMessage);

    public bool IsReviewSettingsLoading
    {
        get => isReviewSettingsLoading;
        private set
        {
            if (SetProperty(ref isReviewSettingsLoading, value))
            {
                OnPropertyChanged(nameof(CanEditReviewSettings));
                OnPropertyChanged(nameof(CanSaveReviewSettings));
            }
        }
    }

    public bool IsReviewSettingsSaving
    {
        get => isReviewSettingsSaving;
        private set
        {
            if (SetProperty(ref isReviewSettingsSaving, value))
            {
                OnPropertyChanged(nameof(CanEditReviewSettings));
                OnPropertyChanged(nameof(CanSaveReviewSettings));
            }
        }
    }

    public bool CanEditReviewSettings => HasOpenCollection && !IsReviewSettingsLoading && !IsReviewSettingsSaving;

    public bool CanSaveReviewSettings =>
        CanEditReviewSettings
        && !HasReviewSettingsValidationMessage;

    private bool TryCreateValidatedReviewSettings(out FlashcardReviewSettingsReadModel settings)
    {
        List<string> errors = [];

        if (!int.TryParse(ReviewSettingsDailyLimitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int reviewLimit)
            || reviewLimit < 0)
        {
            errors.Add("Daily review limit must be greater than or equal to 0.");
        }

        if (!int.TryParse(ReviewSettingsNewCardsLimitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int newCardsLimit)
            || newCardsLimit < 0)
        {
            errors.Add("New card limit must be greater than or equal to 0.");
        }

        if (ReviewSettingsDesiredRetention < FlashcardReviewSettingsReadModel.MinimumDesiredRetention
            || ReviewSettingsDesiredRetention > FlashcardReviewSettingsReadModel.MaximumDesiredRetention)
        {
            errors.Add("Desired retention must be between 0.70 and 0.99.");
        }

        if (errors.Count > 0)
        {
            settings = FlashcardReviewSettingsReadModel.Default;
            ReviewSettingsValidationMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        settings = new FlashcardReviewSettingsReadModel(
            reviewLimit,
            newCardsLimit,
            ReviewSettingsDesiredRetention).Normalize();
        ReviewSettingsValidationMessage = string.Empty;
        return true;
    }

    private void UpdateReviewSettingsValidation()
    {
        if (!HasOpenCollection)
        {
            ReviewSettingsValidationMessage = string.Empty;
            return;
        }

        TryCreateValidatedReviewSettings(out _);
    }

    private void ClearReviewSettingsFeedback(bool clearValidation = true)
    {
        ReviewSettingsStatusMessage = string.Empty;
        ReviewSettingsErrorMessage = string.Empty;
        if (clearValidation)
        {
            ReviewSettingsValidationMessage = string.Empty;
        }
    }
}
