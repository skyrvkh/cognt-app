using System.Globalization;
using CogntApp.Design;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private bool isAutoSaveEnabled;
    private string autoSaveIntervalMinutesText = EditorSettings.Default.AutoSaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
    private string autoSaveSettingsStatusMessage = string.Empty;
    private string autoSaveSettingsValidationMessage = string.Empty;

    public bool IsAutoSaveEnabled
    {
        get => isAutoSaveEnabled;
        set
        {
            if (SetProperty(ref isAutoSaveEnabled, value))
            {
                AutoSaveSettingsStatusMessage = string.Empty;
                OnPropertyChanged(nameof(AutoSaveIntervalSummary));
                OnPropertyChanged(nameof(CanRunAutoSave));
                RaiseEditorSettingsCommandStates();
                AutoSaveSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string AutoSaveIntervalMinutesText
    {
        get => autoSaveIntervalMinutesText;
        set
        {
            if (SetProperty(ref autoSaveIntervalMinutesText, value))
            {
                AutoSaveSettingsStatusMessage = string.Empty;
                ValidateAutoSaveSettings();
                OnPropertyChanged(nameof(AutoSaveInterval));
                OnPropertyChanged(nameof(AutoSaveIntervalSummary));
                OnPropertyChanged(nameof(CanRunAutoSave));
                RaiseEditorSettingsCommandStates();
                AutoSaveSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public TimeSpan AutoSaveInterval => TimeSpan.FromMinutes(GetValidatedAutoSaveIntervalMinutes());

    public string AutoSaveIntervalSummary => IsAutoSaveEnabled
        ? $"Every {GetValidatedAutoSaveIntervalMinutes()} minute(s)"
        : "Off. Notes save when closed.";

    public string AutoSaveSettingsStatusMessage
    {
        get => autoSaveSettingsStatusMessage;
        private set => SetProperty(ref autoSaveSettingsStatusMessage, value);
    }

    public string AutoSaveSettingsValidationMessage
    {
        get => autoSaveSettingsValidationMessage;
        private set
        {
            if (SetProperty(ref autoSaveSettingsValidationMessage, value))
            {
                OnPropertyChanged(nameof(HasAutoSaveSettingsValidationMessage));
                OnPropertyChanged(nameof(CanSaveEditorSettings));
                OnPropertyChanged(nameof(CanRunAutoSave));
            }
        }
    }

    public bool HasAutoSaveSettingsValidationMessage =>
        !string.IsNullOrWhiteSpace(AutoSaveSettingsValidationMessage);

    public bool CanSaveEditorSettings => !HasAutoSaveSettingsValidationMessage;

    public bool CanRunAutoSave => IsAutoSaveEnabled && CanSaveEditorSettings;

    private void InitializeEditorSettings()
    {
        ApplyEditorSettings(editorSettingsService.Load(), save: false, raiseChanged: false);
        AutoSaveSettingsStatusMessage = IsAutoSaveEnabled
            ? $"Auto-save is on every {GetValidatedAutoSaveIntervalMinutes()} minute(s)."
            : "Auto-save is off. Notes save when closed.";
    }

    private void SaveEditorSettings()
    {
        if (!TryGetAutoSaveIntervalMinutes(out int intervalMinutes, out string validationMessage))
        {
            AutoSaveSettingsValidationMessage = validationMessage;
            AutoSaveSettingsStatusMessage = string.Empty;
            return;
        }

        ApplyEditorSettings(
            new EditorSettings(IsAutoSaveEnabled, intervalMinutes),
            save: true,
            raiseChanged: true);
        AutoSaveSettingsStatusMessage = IsAutoSaveEnabled
            ? $"Auto-save will run every {intervalMinutes} minute(s)."
            : "Auto-save is off. Notes save when closed.";
    }

    private void ResetEditorSettings()
    {
        ApplyEditorSettings(EditorSettings.Default, save: true, raiseChanged: true);
        AutoSaveSettingsStatusMessage = "Editor settings reset. Auto-save is off.";
    }

    private void ApplyEditorSettings(EditorSettings settings, bool save, bool raiseChanged)
    {
        EditorSettings normalized = settings.Normalize();
        isAutoSaveEnabled = normalized.IsAutoSaveEnabled;
        autoSaveIntervalMinutesText = normalized.AutoSaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        AutoSaveSettingsValidationMessage = string.Empty;

        OnPropertyChanged(nameof(IsAutoSaveEnabled));
        OnPropertyChanged(nameof(AutoSaveIntervalMinutesText));
        OnPropertyChanged(nameof(AutoSaveInterval));
        OnPropertyChanged(nameof(AutoSaveIntervalSummary));
        OnPropertyChanged(nameof(CanSaveEditorSettings));
        OnPropertyChanged(nameof(CanRunAutoSave));
        RaiseEditorSettingsCommandStates();

        if (save)
        {
            editorSettingsService.Save(normalized);
        }

        if (raiseChanged)
        {
            AutoSaveSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ValidateAutoSaveSettings()
    {
        AutoSaveSettingsValidationMessage = TryGetAutoSaveIntervalMinutes(out _, out string validationMessage)
            ? string.Empty
            : validationMessage;
    }

    private int GetValidatedAutoSaveIntervalMinutes()
    {
        return TryGetAutoSaveIntervalMinutes(out int intervalMinutes, out _)
            ? intervalMinutes
            : EditorSettings.Default.AutoSaveIntervalMinutes;
    }

    private bool TryGetAutoSaveIntervalMinutes(out int intervalMinutes, out string validationMessage)
    {
        validationMessage = string.Empty;
        if (!int.TryParse(AutoSaveIntervalMinutesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalMinutes))
        {
            validationMessage = "Enter a whole number of minutes.";
            return false;
        }

        if (intervalMinutes < EditorSettings.MinimumAutoSaveIntervalMinutes
            || intervalMinutes > EditorSettings.MaximumAutoSaveIntervalMinutes)
        {
            validationMessage = $"Use a value from {EditorSettings.MinimumAutoSaveIntervalMinutes} to {EditorSettings.MaximumAutoSaveIntervalMinutes} minutes.";
            return false;
        }

        return true;
    }

    private void RaiseEditorSettingsCommandStates()
    {
        if (SaveEditorSettingsCommand is RelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }
    }
}
