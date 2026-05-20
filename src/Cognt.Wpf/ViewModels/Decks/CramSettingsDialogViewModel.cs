using System.Globalization;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace CogntApp.ViewModels.Decks;

public sealed class CramSettingsDialogViewModel : ObservableObject
{
    private string requiredPassCountText = CrammingSettings.Default.RequiredPassCount.ToString(CultureInfo.InvariantCulture);
    private bool countHardAsPass = CrammingSettings.Default.CountHardAsPass;
    private bool countGoodAsPass = CrammingSettings.Default.CountGoodAsPass;
    private bool countEasyAsPass = CrammingSettings.Default.CountEasyAsPass;
    private bool useCumulativeCounting = CrammingSettings.Default.PassCountingMode == CramPassCountingMode.Cumulative;
    private bool useConsecutiveCounting = CrammingSettings.Default.PassCountingMode == CramPassCountingMode.Consecutive;
    private string retryAfterOtherCardsText = CrammingSettings.Default.RetryAfterOtherCards.ToString(CultureInfo.InvariantCulture);
    private bool shuffleRemainingCards = CrammingSettings.Default.ShuffleRemainingCards;
    private bool affectNormalSchedule = CrammingSettings.Default.AffectNormalSchedule;
    private string validationMessage = string.Empty;

    public CramSettingsDialogViewModel()
    {
        StartCommand = new RelayCommand(Start);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<CramSettingsAcceptedEventArgs>? StartRequested;

    public event EventHandler? CancelRequested;

    public ICommand StartCommand { get; }

    public ICommand CancelCommand { get; }

    public string RequiredPassCountText
    {
        get => requiredPassCountText;
        set
        {
            if (SetProperty(ref requiredPassCountText, value))
            {
                ClearValidation();
            }
        }
    }

    public bool CountHardAsPass
    {
        get => countHardAsPass;
        set
        {
            if (SetProperty(ref countHardAsPass, value))
            {
                ClearValidation();
            }
        }
    }

    public bool CountGoodAsPass
    {
        get => countGoodAsPass;
        set
        {
            if (SetProperty(ref countGoodAsPass, value))
            {
                ClearValidation();
            }
        }
    }

    public bool CountEasyAsPass
    {
        get => countEasyAsPass;
        set
        {
            if (SetProperty(ref countEasyAsPass, value))
            {
                ClearValidation();
            }
        }
    }

    public bool UseCumulativeCounting
    {
        get => useCumulativeCounting;
        set
        {
            if (!SetProperty(ref useCumulativeCounting, value))
            {
                return;
            }

            if (value)
            {
                UseConsecutiveCounting = false;
            }

            ClearValidation();
        }
    }

    public bool UseConsecutiveCounting
    {
        get => useConsecutiveCounting;
        set
        {
            if (!SetProperty(ref useConsecutiveCounting, value))
            {
                return;
            }

            if (value)
            {
                UseCumulativeCounting = false;
            }

            ClearValidation();
        }
    }

    public string RetryAfterOtherCardsText
    {
        get => retryAfterOtherCardsText;
        set
        {
            if (SetProperty(ref retryAfterOtherCardsText, value))
            {
                ClearValidation();
            }
        }
    }

    public bool ShuffleRemainingCards
    {
        get => shuffleRemainingCards;
        set => SetProperty(ref shuffleRemainingCards, value);
    }

    public bool AffectNormalSchedule
    {
        get => affectNormalSchedule;
        set => SetProperty(ref affectNormalSchedule, value);
    }

    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (SetProperty(ref validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    internal bool TryCreateSettings(out CrammingSettings settings)
    {
        List<string> errors = [];
        if (!int.TryParse(RequiredPassCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requiredPassCount))
        {
            errors.Add("Required successful ratings must be a whole number.");
        }

        if (!int.TryParse(RetryAfterOtherCardsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int retryAfterOtherCards))
        {
            errors.Add("Retry spacing must be a whole number.");
        }

        CramPassCountingMode countingMode = UseCumulativeCounting
            ? CramPassCountingMode.Cumulative
            : CramPassCountingMode.Consecutive;
        settings = new CrammingSettings(
            requiredPassCount,
            CountHardAsPass,
            CountGoodAsPass,
            CountEasyAsPass,
            countingMode,
            retryAfterOtherCards,
            ShuffleRemainingCards,
            AffectNormalSchedule);

        errors.AddRange(settings.Validate());
        if (errors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors.Distinct());
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }

    private void Start()
    {
        if (TryCreateSettings(out CrammingSettings settings))
        {
            StartRequested?.Invoke(this, new CramSettingsAcceptedEventArgs(settings));
        }
    }

    private void ClearValidation()
    {
        ValidationMessage = string.Empty;
    }
}

public sealed class CramSettingsAcceptedEventArgs : EventArgs
{
    public CramSettingsAcceptedEventArgs(CrammingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
    }

    public CrammingSettings Settings { get; }
}
