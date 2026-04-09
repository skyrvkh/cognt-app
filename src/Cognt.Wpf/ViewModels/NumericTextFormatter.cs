using System.Globalization;

namespace CogntApp.ViewModels;

internal static class NumericTextFormatter
{
    public static string Format(int count)
    {
        return count.ToString("N0", CultureInfo.InvariantCulture);
    }
}
