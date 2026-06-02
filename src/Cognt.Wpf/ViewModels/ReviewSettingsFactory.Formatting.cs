using System.Text;

namespace CogntApp.ViewModels;

internal static partial class ReviewSettingsFactory
{
    private static void AppendDelimitedValue(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(value);
    }
}
