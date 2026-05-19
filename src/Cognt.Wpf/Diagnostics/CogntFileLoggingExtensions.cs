using Microsoft.Extensions.Logging;

namespace CogntApp.Diagnostics;

internal static class CogntFileLoggingExtensions
{
    internal static ILoggingBuilder AddCogntFile(this ILoggingBuilder builder, string filePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        builder.AddProvider(new CogntFileLoggerProvider(filePath));
        return builder;
    }
}
