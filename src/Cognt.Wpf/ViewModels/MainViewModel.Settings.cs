
namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public bool CanSaveSettings =>
        CanSaveEditorSettings
        && (!HasOpenCollection || (CanSaveGraphSettings && CanSaveReviewSettings));

    private void SaveSettings()
    {
        if (!CanSaveSettings)
        {
            return;
        }

        SaveEditorSettings();

        if (!HasOpenCollection)
        {
            return;
        }

        SaveGraphSettings();
        SaveReviewSettings();
    }

    private void RaiseSettingsCommandStates()
    {
        OnPropertyChanged(nameof(CanSaveSettings));

        if (SaveSettingsCommand is RelayCommand saveSettingsCommand)
        {
            saveSettingsCommand.RaiseCanExecuteChanged();
        }
    }
}
