using System.Globalization;
using System.IO;

namespace CogntApp.Diagnostics;

internal static class AppDiagnosticPaths
{
    internal static string LogDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cognt",
        "logs");

    internal static string CreateSessionLogFilePath()
    {
        Directory.CreateDirectory(LogDirectory);
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return System.IO.Path.Combine(LogDirectory, $"cognt-{timestamp}.log");
    }
}
