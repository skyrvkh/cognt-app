using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private string reviewSettingsDesiredRetentionText = ReviewSettingsFactory.FormatRetention(FlashcardReviewSettingsReadModel.DefaultDesiredRetention);
    private string reviewSettingsLearningStepsText = ReviewSettingsFactory.FormatSteps(FlashcardReviewSettingsReadModel.DefaultLearningSteps);
    private string reviewSettingsRelearningStepsText = ReviewSettingsFactory.FormatSteps(FlashcardReviewSettingsReadModel.DefaultRelearningSteps);
    private bool isFsrsOptimizationEnabled = FlashcardReviewSettingsReadModel.DefaultIsOptimizationEnabled;
    private bool isCramEnabled = FlashcardReviewSettingsReadModel.DefaultIsCramEnabled;
    private bool showFlashcardsInNavigationTree = FlashcardReviewSettingsReadModel.DefaultShowFlashcardsInNavigationTree;
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
                reviewSettingsDesiredRetentionText = ReviewSettingsFactory.FormatRetention(normalized);
                ClearReviewSettingsFeedback(clearValidation: false);
                OnPropertyChanged(nameof(ReviewSettingsDesiredRetentionText));
                OnPropertyChanged(nameof(ReviewSettingsDesiredRetentionDisplay));
                UpdateReviewSettingsValidation();
            }
        }
    }

    public string ReviewSettingsDesiredRetentionDisplay =>
        $"{ReviewSettingsDesiredRetention:P0}";

    public string ReviewSettingsDesiredRetentionText
    {
        get => reviewSettingsDesiredRetentionText;
        set
        {
            if (!SetProperty(ref reviewSettingsDesiredRetentionText, value))
            {
                return;
            }

            ClearReviewSettingsFeedback(clearValidation: false);
            if (ReviewSettingsFactory.TryParseRetention(value, out double retention))
            {
                reviewSettingsDesiredRetention = retention;
                OnPropertyChanged(nameof(ReviewSettingsDesiredRetention));
                OnPropertyChanged(nameof(ReviewSettingsDesiredRetentionDisplay));
            }

            UpdateReviewSettingsValidation();
        }
    }

    public string ReviewSettingsLearningStepsText
    {
        get => reviewSettingsLearningStepsText;
        set
        {
            if (SetProperty(ref reviewSettingsLearningStepsText, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public string ReviewSettingsRelearningStepsText
    {
        get => reviewSettingsRelearningStepsText;
        set
        {
            if (SetProperty(ref reviewSettingsRelearningStepsText, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public bool IsFsrsOptimizationEnabled
    {
        get => isFsrsOptimizationEnabled;
        set
        {
            if (SetProperty(ref isFsrsOptimizationEnabled, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public bool IsCramEnabled
    {
        get => isCramEnabled;
        set
        {
            if (SetProperty(ref isCramEnabled, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
            }
        }
    }

    public bool ShowFlashcardsInNavigationTree
    {
        get => showFlashcardsInNavigationTree;
        set
        {
            if (SetProperty(ref showFlashcardsInNavigationTree, value))
            {
                ClearReviewSettingsFeedback(clearValidation: false);
                UpdateReviewSettingsValidation();
                if (HasOpenCollection && !IsReviewSettingsLoading)
                {

                    RefreshNotesTree(preserveExpansion: true);
                }
            }
        }
    }

    public ObservableCollection<FsrsParameterSettingViewModel> ReviewSettingsFsrsParameters { get; } = [];

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
                RaiseSettingsCommandStates();
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
                RaiseSettingsCommandStates();
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
                RaiseSettingsCommandStates();
            }
        }
    }

    public bool CanEditReviewSettings => HasOpenCollection && !IsReviewSettingsLoading && !IsReviewSettingsSaving;

    public bool CanSaveReviewSettings =>
        CanEditReviewSettings
        && !HasReviewSettingsValidationMessage;

    private bool TryCreateValidatedReviewSettings(out FlashcardReviewSettingsReadModel settings)
    {
        ReviewSettingsDraft draft = new(
            ReviewSettingsDailyLimitText,
            ReviewSettingsNewCardsLimitText,
            ReviewSettingsDesiredRetentionText,
            ReviewSettingsLearningStepsText,
            ReviewSettingsRelearningStepsText,
            IsFsrsOptimizationEnabled,
            ReviewSettingsFsrsParameters,
            IsCramEnabled,
            ShowFlashcardsInNavigationTree);

        if (!ReviewSettingsFactory.TryCreate(draft, out settings, out string validationMessage))
        {
            ReviewSettingsValidationMessage = validationMessage;
            return false;
        }

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

    private void InitializeReviewParameterSettings()
    {
        string[] labels =
        [
            "initial stability (Again)",
            "initial stability (Hard)",
            "initial stability (Good)",
            "initial stability (Easy)",
            "initial difficulty (Good)",
            "initial difficulty (multiplier)",
            "difficulty (multiplier)",
            "difficulty (multiplier)",
            "stability (exponent)",
            "stability (negative power)",
            "stability (exponent)",
            "fail stability (multiplier)",
            "fail stability (negative power)",
            "fail stability (power)",
            "fail stability (exponent)",
            "stability (multiplier for Hard)",
            "stability (multiplier for Easy)",
            "short-term stability (exponent)",
            "short-term stability (exponent)",
            "short-term last-stability (exponent)",
            "decay",
        ];

        ReviewSettingsFsrsParameters.Clear();
        for (int i = 0; i < labels.Length; i++)
        {
            FsrsParameterSettingViewModel parameter = new(
                i,
                labels[i],
                FlashcardReviewSettingsReadModel.MinimumFsrsParameters[i],
                FlashcardReviewSettingsReadModel.MaximumFsrsParameters[i]);
            parameter.PropertyChanged += FsrsParameter_PropertyChanged;
            ReviewSettingsFsrsParameters.Add(parameter);
        }
    }

    private void ApplyFsrsParameterSettings(IReadOnlyList<double>? parameters)
    {
        for (int i = 0; i < ReviewSettingsFsrsParameters.Count; i++)
        {
            ReviewSettingsFsrsParameters[i].ValueText = parameters is not null && i < parameters.Count
                ? parameters[i].ToString("G17", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }

    private void FsrsParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FsrsParameterSettingViewModel.ValueText))
        {
            return;
        }

        ClearReviewSettingsFeedback(clearValidation: false);
        UpdateReviewSettingsValidation();
    }
}

public sealed class FsrsParameterSettingViewModel : ObservableObject
{
    private string valueText = string.Empty;

    internal FsrsParameterSettingViewModel(int index, string label, double minimumValue, double maximumValue)
    {
        Index = index;
        Label = label;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
    }

    public int Index { get; }

    public string Label { get; }

    public double MinimumValue { get; }

    public double MaximumValue { get; }

    public string DisplayName => $"{Index}. {Label}";

    public string RangeDisplay => $"Range {MinimumValue:G17} - {MaximumValue:G17}";

    public string ValueText
    {
        get => valueText;
        set => SetProperty(ref valueText, value);
    }
}
