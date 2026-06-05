using System.Globalization;
using CogntApp.Design;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private bool isApplyingEditorSettings;
    private bool isAutoSaveEnabled;
    private bool isSaveOnCloseEnabled = EditorSettings.Default.IsSaveOnCloseEnabled;
    private string autoSaveIntervalMinutesText = EditorSettings.Default.AutoSaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
    private string autoSaveSettingsStatusMessage = string.Empty;
    private string autoSaveSettingsValidationMessage = string.Empty;
    private string interfaceFontFamilyText = EditorSettings.Default.InterfaceFontFamily;
    private string displayFontFamilyText = EditorSettings.Default.DisplayFontFamily;
    private string readingFontFamilyText = EditorSettings.Default.ReadingFontFamily;
    private string codeFontFamilyText = EditorSettings.Default.CodeFontFamily;
    private string editorTextFontSizeText = EditorSettings.Default.EditorTextFontSize.ToString("0", CultureInfo.InvariantCulture);
    private string editorHeadingFontSizeText = EditorSettings.Default.EditorHeadingFontSize.ToString("0", CultureInfo.InvariantCulture);
    private string editorCodeFontSizeText = EditorSettings.Default.EditorCodeFontSize.ToString("0", CultureInfo.InvariantCulture);
    private string editorZoomPercentText = EditorSettings.Default.EditorZoomPercent.ToString("0", CultureInfo.InvariantCulture);

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
                ValidateEditorSettings();
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

    public string InterfaceFontFamilyText
    {
        get => interfaceFontFamilyText;
        set => SetFontFamilyText(ref interfaceFontFamilyText, value, nameof(InterfaceFontFamilyText));
    }

    public string DisplayFontFamilyText
    {
        get => displayFontFamilyText;
        set => SetFontFamilyText(ref displayFontFamilyText, value, nameof(DisplayFontFamilyText));
    }

    public string ReadingFontFamilyText
    {
        get => readingFontFamilyText;
        set => SetFontFamilyText(ref readingFontFamilyText, value, nameof(ReadingFontFamilyText));
    }

    public string CodeFontFamilyText
    {
        get => codeFontFamilyText;
        set => SetFontFamilyText(ref codeFontFamilyText, value, nameof(CodeFontFamilyText));
    }

    public string EditorTextFontSizeText
    {
        get => editorTextFontSizeText;
        set => SetEditorNumberText(ref editorTextFontSizeText, value, nameof(EditorTextFontSizeText));
    }

    public string EditorHeadingFontSizeText
    {
        get => editorHeadingFontSizeText;
        set => SetEditorNumberText(ref editorHeadingFontSizeText, value, nameof(EditorHeadingFontSizeText));
    }

    public string EditorCodeFontSizeText
    {
        get => editorCodeFontSizeText;
        set => SetEditorNumberText(ref editorCodeFontSizeText, value, nameof(EditorCodeFontSizeText));
    }

    public string EditorZoomPercentText
    {
        get => editorZoomPercentText;
        set => SetEditorNumberText(ref editorZoomPercentText, value, nameof(EditorZoomPercentText));
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

    private void InitializeFontFamilyOptions()
    {
        FontFamilyOptions.Clear();
        foreach (string familyName in EnumerateFontFamilyOptions())
        {
            FontFamilyOptions.Add(familyName);
        }
    }

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
        if (!TryCreateValidatedEditorSettings(out EditorSettings settings, out string validationMessage))
        {
            AutoSaveSettingsValidationMessage = validationMessage;
            AutoSaveSettingsStatusMessage = string.Empty;
            return;
        }

        ApplyEditorSettings(settings, save: true, raiseChanged: true);
        AutoSaveSettingsStatusMessage = HasOpenCollection
            ? $"Editor preferences saved for {CurrentCollectionName}."
            : "Editor preferences saved.";
    }

    private void ResetEditorSettings()
    {
        ApplyEditorSettings(EditorSettings.Default, save: true, raiseChanged: true);
        AutoSaveSettingsStatusMessage = "Editor preferences reset.";
    }

    internal EditorSettings CreateEditorSettingsSnapshot()
    {
        ApplicationTheme theme = selectedThemeOption?.Theme ?? ApplicationTheme.Light;

        return EditorSettingsFactory.CreateSnapshot(CreateEditorSettingsDraft(theme));
    }

    private EditorSettingsDraft CreateEditorSettingsDraft(ApplicationTheme theme)
    {
        return new EditorSettingsDraft(
            IsAutoSaveEnabled,
            AutoSaveIntervalMinutesText,
            IsSaveOnCloseEnabled,
            GlobalTextFontSizeText,
            theme,
            InterfaceFontFamilyText,
            DisplayFontFamilyText,
            ReadingFontFamilyText,
            CodeFontFamilyText,
            EditorTextFontSizeText,
            EditorHeadingFontSizeText,
            EditorCodeFontSizeText,
            EditorZoomPercentText);
    }

    private void ApplyEditorSettings(EditorSettings settings, bool save, bool raiseChanged)
    {
        EditorSettings normalized = settings.Normalize();
        isApplyingEditorSettings = true;
        try
        {
            isAutoSaveEnabled = normalized.IsAutoSaveEnabled;
            isSaveOnCloseEnabled = normalized.IsSaveOnCloseEnabled;
            autoSaveIntervalMinutesText = normalized.AutoSaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
            interfaceFontFamilyText = normalized.InterfaceFontFamily;
            displayFontFamilyText = normalized.DisplayFontFamily;
            readingFontFamilyText = normalized.ReadingFontFamily;
            codeFontFamilyText = normalized.CodeFontFamily;
            editorTextFontSizeText = FormatWholeNumber(normalized.EditorTextFontSize);
            editorHeadingFontSizeText = FormatWholeNumber(normalized.EditorHeadingFontSize);
            editorCodeFontSizeText = FormatWholeNumber(normalized.EditorCodeFontSize);
            editorZoomPercentText = FormatWholeNumber(normalized.EditorZoomPercent);
            AutoSaveSettingsValidationMessage = string.Empty;

            ApplyGlobalTextFontSize(normalized.GlobalTextFontSize);
            ApplyThemePreference(normalized.Theme);
            TypographyResourceApplier.Apply(normalized);
        }
        finally
        {
            isApplyingEditorSettings = false;
        }

        RaiseEditorSettingsPropertyChanges();
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

    private void SetFontFamilyText(ref string field, string value, string propertyName)
    {
        value ??= string.Empty;
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);

        HandleEditorVisualSettingsChanged();
    }

    private void SetEditorNumberText(ref string field, string value, string propertyName)
    {
        value ??= string.Empty;
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        HandleEditorVisualSettingsChanged();
    }

    private void HandleEditorVisualSettingsChanged()
    {
        if (isApplyingEditorSettings)
        {
            return;
        }

        AutoSaveSettingsStatusMessage = string.Empty;
        ValidateEditorSettings();
        TypographyResourceApplier.Apply(CreateEditorSettingsSnapshot());
        RaiseEditorSettingsCommandStates();
    }

    private void ValidateEditorSettings()
    {
        bool isValid = TryCreateValidatedEditorSettings(out _, out string validationMessage);
        AutoSaveSettingsValidationMessage = isValid ? string.Empty : validationMessage;
    }

    private bool TryCreateValidatedEditorSettings(out EditorSettings settings, out string validationMessage)
    {
        bool isValid = EditorSettingsFactory.TryCreateValidated(
            CreateEditorSettingsDraft(SelectedThemeOption.Theme),
            out settings,
            out validationMessage);
        return isValid;
    }

    private int GetValidatedAutoSaveIntervalMinutes()
    {
        return EditorSettingsFactory.GetValidatedAutoSaveIntervalMinutes(AutoSaveIntervalMinutesText);
    }

    private void RaiseEditorSettingsCommandStates()
    {
        RaiseSettingsCommandStates();

        if (SaveEditorSettingsCommand is RelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void RaiseEditorSettingsPropertyChanges()
    {
        OnPropertyChanged(nameof(IsAutoSaveEnabled));
        OnPropertyChanged(nameof(IsSaveOnCloseEnabled));
        OnPropertyChanged(nameof(AutoSaveIntervalMinutesText));
        OnPropertyChanged(nameof(AutoSaveInterval));
        OnPropertyChanged(nameof(InterfaceFontFamilyText));
        OnPropertyChanged(nameof(DisplayFontFamilyText));
        OnPropertyChanged(nameof(ReadingFontFamilyText));
        OnPropertyChanged(nameof(CodeFontFamilyText));
        OnPropertyChanged(nameof(EditorTextFontSizeText));
        OnPropertyChanged(nameof(EditorHeadingFontSizeText));
        OnPropertyChanged(nameof(EditorCodeFontSizeText));
        OnPropertyChanged(nameof(EditorZoomPercentText));
        OnPropertyChanged(nameof(CanSaveEditorSettings));
        OnPropertyChanged(nameof(CanRunAutoSave));
        OnPropertyChanged(nameof(CanSaveOnClose));
    }

    private void ApplyGlobalTextFontSize(double fontSize)
    {
        double normalized = Math.Clamp(
            Math.Round(fontSize, 0),
            EditorSettings.MinimumGlobalTextFontSize,
            EditorSettings.MaximumGlobalTextFontSize);
        globalTextFontSize = normalized;
        globalTextFontSizeText = FormatWholeNumber(normalized);
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

    private static string FormatWholeNumber(double value)
    {
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> EnumerateFontFamilyOptions()
    {
        return BundledFontFamilyCatalog.Families;
    }
}
