using System.Globalization;
using System.Text;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

internal static partial class ReviewSettingsFactory
{
    public static string FormatFsrsParameters(IReadOnlyList<double>? parameters)
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

    private static bool TryParseFsrsParameters(
        IReadOnlyList<FsrsParameterSettingViewModel> parameterSettings,
        out double[]? parameters,
        out string validationMessage)
    {
        parameters = null;
        validationMessage = string.Empty;

        bool hasAnyParameter = parameterSettings.Any(parameter => !string.IsNullOrWhiteSpace(parameter.ValueText));
        if (!hasAnyParameter)
        {
            return true;
        }

        if (parameterSettings.Count != FlashcardReviewSettingsReadModel.FsrsParameterCount)
        {
            validationMessage = $"FSRS parameters must contain exactly {FlashcardReviewSettingsReadModel.FsrsParameterCount} values.";
            return false;
        }

        double[] parsed = new double[parameterSettings.Count];
        foreach (FsrsParameterSettingViewModel parameterSetting in parameterSettings)
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
}
