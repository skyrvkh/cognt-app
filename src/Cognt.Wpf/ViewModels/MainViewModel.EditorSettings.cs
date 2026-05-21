using System.Globalization;
using CogntApp.Design;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private bool isAutoSaveEnabled;
    private bool isSaveOnCloseEnabled = EditorSettings.Default.IsSaveOnCloseEnabled;
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
                OnPropertyChanged(nameof(CanRunAutoSave));
                RaiseEditorSettingsCommandStates();
                AutoSaveSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public TimeSpan AutoSaveInterval => TimeSpan.FromMinutes(GetValidatedAutoSaveIntervalMinutes());

    public bool IsSaveOnCloseEnabled
    {
        get => isSaveOnCloseEnabled;
        set
        {
            if (SetProperty(ref isSaveOnCloseEnabled, value))
            {
                AutoSaveSettingsStatusMessage = string.Empty;
                RaiseEditorSettingsCommandStates();
                AutoSaveSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

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

    public bool CanSaveOnClose => IsSaveOnCloseEnabled && CanSaveEditorSettings;

    private void InitializeEditorSettings()
    {
        RefreshEditorSettings();
    }

    private void RefreshEditorSettings()
    {
        ApplyEditorSettings(editorSettingsService.Load(OpenCollectionPath), save: false, raiseChanged: true);
        AutoSaveSettingsStatusMessage = string.Empty;
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
            new EditorSettings(
                IsAutoSaveEnabled,
                intervalMinutes,
                IsSaveOnCloseEnabled,
                GlobalTextFontSize,
                SelectedThemeOption.Theme),
            save: true,
            raiseChanged: true);
        AutoSaveSettingsStatusMessage = HasOpenCollection
            ? $"Editor preferences saved for {CurrentCollectionName}."
            : "Editor preferences saved.";
    }

    private void ResetEditorSettings()
    {
        ApplyEditorSettings(EditorSettings.Default, save: true, raiseChanged: true);
        AutoSaveSettingsStatusMessage = "Editor preferences reset.";
    }

    private void ApplyEditorSettings(EditorSettings settings, bool save, bool raiseChanged)
    {
        EditorSettings normalized = settings.Normalize();
        isAutoSaveEnabled = normalized.IsAutoSaveEnabled;
        isSaveOnCloseEnabled = normalized.IsSaveOnCloseEnabled;
        autoSaveIntervalMinutesText = normalized.AutoSaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        AutoSaveSettingsValidationMessage = string.Empty;

        ApplyGlobalTextFontSize(normalized.GlobalTextFontSize);
        ApplyThemePreference(normalized.Theme);

        OnPropertyChanged(nameof(IsAutoSaveEnabled));
        OnPropertyChanged(nameof(IsSaveOnCloseEnabled));
        OnPropertyChanged(nameof(AutoSaveIntervalMinutesText));
        OnPropertyChanged(nameof(AutoSaveInterval));
        OnPropertyChanged(nameof(CanSaveEditorSettings));
        OnPropertyChanged(nameof(CanRunAutoSave));
        OnPropertyChanged(nameof(CanSaveOnClose));
        RaiseEditorSettingsCommandStates();

        if (save)
        {
            editorSettingsService.Save(normalized, OpenCollectionPath);
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
        RaiseSettingsCommandStates();

        if (SaveEditorSettingsCommand is RelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void ApplyGlobalTextFontSize(double fontSize)
    {
        double normalized = Math.Clamp(
            Math.Round(fontSize, 0),
            EditorSettings.MinimumGlobalTextFontSize,
            EditorSettings.MaximumGlobalTextFontSize);
        globalTextFontSize = normalized;
        globalTextFontSizeText = normalized.ToString("0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(GlobalTextFontSize));
        OnPropertyChanged(nameof(GlobalTextFontSizeText));
    }

    private void ApplyThemePreference(ApplicationTheme theme)
    {
        ThemeOptionViewModel option = ThemeOptions.FirstOrDefault(candidate => candidate.Theme == theme)
            ?? ThemeOptions.First(candidate => candidate.Theme == ApplicationTheme.Light);

        if (!ReferenceEquals(selectedThemeOption, option))
        {
            selectedThemeOption = option;
            themeService.ApplyTheme(option.Theme);
            OnPropertyChanged(nameof(SelectedThemeOption));
            OnPropertyChanged(nameof(CurrentThemeDescription));
            OnPropertyChanged(nameof(CurrentThemeName));
            OnPropertyChanged(nameof(IsGraphiteTheme));
        }
    }
}
