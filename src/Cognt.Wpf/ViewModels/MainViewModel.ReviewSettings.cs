using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
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
    private string reviewSettingsDesiredRetentionText = FormatReviewRetention(FlashcardReviewSettingsReadModel.DefaultDesiredRetention);
    private string reviewSettingsLearningStepsText = FormatReviewSteps(FlashcardReviewSettingsReadModel.DefaultLearningSteps);
    private string reviewSettingsRelearningStepsText = FormatReviewSteps(FlashcardReviewSettingsReadModel.DefaultRelearningSteps);
    private bool isFsrsOptimizationEnabled = FlashcardReviewSettingsReadModel.DefaultIsOptimizationEnabled;
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
                reviewSettingsDesiredRetentionText = FormatReviewRetention(normalized);
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
            if (TryParseRetention(value, out double retention))
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

        if (!TryParseRetention(ReviewSettingsDesiredRetentionText, out double desiredRetention)
            || desiredRetention < FlashcardReviewSettingsReadModel.MinimumDesiredRetention
            || desiredRetention > FlashcardReviewSettingsReadModel.MaximumDesiredRetention)
        {
            errors.Add("Desired retention must be between 70% and 99%.");
        }

        if (!TryParseReviewSteps(ReviewSettingsLearningStepsText, out TimeSpan[] learningSteps))
        {
            errors.Add("Learning steps must use positive intervals such as 1m, 10m, 1h, or 1d.");
        }

        if (!TryParseReviewSteps(ReviewSettingsRelearningStepsText, out TimeSpan[] relearningSteps))
        {
            errors.Add("Relearning steps must use positive intervals such as 10m, 1h, or 1d.");
        }

        if (!TryParseFsrsParameters(out double[]? fsrsParameters, out string parameterError))
        {
            errors.Add(parameterError);
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
            desiredRetention,
            learningSteps,
            relearningSteps,
            IsFsrsOptimizationEnabled,
            fsrsParameters).Normalize();
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

    private static string FormatReviewRetention(double retention)
    {
        double percent = Math.Clamp(retention, 0, 1) * 100;
        return percent.ToString("0", CultureInfo.InvariantCulture);
    }

    private static bool TryParseRetention(string? value, out double retention)
    {
        retention = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().TrimEnd('%').Trim();
        if (!double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out double parsed))
        {
            return false;
        }

        retention = parsed > 1
            ? parsed / 100d
            : parsed;
        return true;
    }

    private static string FormatReviewSteps(IReadOnlyList<TimeSpan>? steps)
    {
        if (steps is null || steps.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (TimeSpan step in steps)
        {
            if (step <= TimeSpan.Zero)
            {
                continue;
            }

            AppendDelimitedValue(builder, FormatReviewStep(step));
        }

        return builder.ToString();
    }

    private static string FormatFsrsParameters(IReadOnlyList<double>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (double parameter in parameters)
        {
            AppendDelimitedValue(builder, parameter.ToString("G17", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void AppendDelimitedValue(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(value);
    }

    private static string FormatReviewStep(TimeSpan step)
    {
        if (step.TotalDays >= 1 && IsWholeNumber(step.TotalDays))
        {
            return $"{step.TotalDays:0}d";
        }

        if (step.TotalHours >= 1 && IsWholeNumber(step.TotalHours))
        {
            return $"{step.TotalHours:0}h";
        }

        if (step.TotalMinutes >= 1 && IsWholeNumber(step.TotalMinutes))
        {
            return $"{step.TotalMinutes:0}m";
        }

        return $"{Math.Max(1, Math.Round(step.TotalSeconds)):0}s";
    }

    private static bool IsWholeNumber(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001d;
    }

    private static bool TryParseReviewSteps(string? value, out TimeSpan[] steps)
    {
        steps = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens = value.Split(
            [',', ';', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<TimeSpan> parsedSteps = [];

        foreach (string token in tokens)
        {
            if (!TryParseReviewStep(token, out TimeSpan step))
            {
                return false;
            }

            parsedSteps.Add(step);
        }

        if (parsedSteps.Count == 0)
        {
            return false;
        }

        steps = parsedSteps.ToArray();
        return true;
    }

    private static bool TryParseReviewStep(string token, out TimeSpan step)
    {
        step = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string normalized = token.Trim().ToLowerInvariant();
        char suffix = normalized[^1];
        string numberText = char.IsLetter(suffix)
            ? normalized[..^1]
            : normalized;

        if (!double.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out double number)
            || number <= 0)
        {
            return false;
        }

        step = suffix switch
        {
            's' => TimeSpan.FromSeconds(number),
            'm' => TimeSpan.FromMinutes(number),
            'h' => TimeSpan.FromHours(number),
            'd' => TimeSpan.FromDays(number),
            _ when !char.IsLetter(suffix) => TimeSpan.FromMinutes(number),
            _ => TimeSpan.Zero,
        };

        return step > TimeSpan.Zero;
    }

    private bool TryParseFsrsParameters(
        out double[]? parameters,
        out string validationMessage)
    {
        parameters = null;
        validationMessage = string.Empty;

        bool hasAnyParameter = ReviewSettingsFsrsParameters.Any(parameter => !string.IsNullOrWhiteSpace(parameter.ValueText));
        if (!hasAnyParameter)
        {
            return true;
        }

        if (ReviewSettingsFsrsParameters.Count != FlashcardReviewSettingsReadModel.FsrsParameterCount)
        {
            validationMessage = $"FSRS parameters must contain exactly {FlashcardReviewSettingsReadModel.FsrsParameterCount} values.";
            return false;
        }

        double[] parsed = new double[ReviewSettingsFsrsParameters.Count];
        foreach (FsrsParameterSettingViewModel parameterSetting in ReviewSettingsFsrsParameters)
        {
            int index = parameterSetting.Index;
            if (string.IsNullOrWhiteSpace(parameterSetting.ValueText))
            {
                validationMessage = $"{parameterSetting.DisplayName} is required when custom FSRS parameters are used.";
                return false;
            }

            if (!double.TryParse(parameterSetting.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parameter))
            {
                validationMessage = $"{parameterSetting.DisplayName} must be a number.";
                return false;
            }

            if (!FlashcardReviewSettingsReadModel.IsValidFsrsParameter(index, parameter))
            {
                validationMessage =
                    $"{parameterSetting.DisplayName} must be between {FlashcardReviewSettingsReadModel.MinimumFsrsParameters[index]:G17} and {FlashcardReviewSettingsReadModel.MaximumFsrsParameters[index]:G17}.";
                return false;
            }

            parsed[index] = parameter;
        }

        parameters = parsed;
        return true;
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
