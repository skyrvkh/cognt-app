using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CogntApp.Diagnostics;

internal static class CrashSafeLog
{
    private static readonly object SyncRoot = new();
    private static string? sessionLogPath;

    private static string MarkerPath => Path.Combine(AppDiagnosticPaths.LogDirectory, "cognt-active-session.txt");

    internal static void StartSession(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        lock (SyncRoot)
        {
            Directory.CreateDirectory(AppDiagnosticPaths.LogDirectory);
            sessionLogPath = logFilePath;
            string? previousMarker = TryReadAllText(MarkerPath);
            if (!string.IsNullOrWhiteSpace(previousMarker)
                && !previousMarker.Contains("Status=CleanExit", StringComparison.OrdinalIgnoreCase))
            {
                string previousLogPath = TryReadMarkerValue(previousMarker, "LogFile");
                string message = CreateLogEntry(
                    "CRITICAL",
                    "CogntApp.Diagnostics.CrashSafeLog",
                    "Previous session did not reach clean application exit."
                    + Environment.NewLine
                    + previousMarker.Trim());
                AppendThrough(logFilePath, message);
                if (!string.IsNullOrWhiteSpace(previousLogPath)
                    && !string.Equals(previousLogPath, logFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    AppendThrough(previousLogPath, message);
                }
            }

            WriteMarker("Running", exitCode: null);
            AppendThrough(
                logFilePath,
                CreateLogEntry(
                    "INFORMATION",
                    "CogntApp.Diagnostics.CrashSafeLog",
                    $"Crash-safe session marker started. MarkerPath={MarkerPath} ProcessId={Environment.ProcessId}"));
        }
    }

    internal static void MarkCleanExit(int exitCode)
    {
        lock (SyncRoot)
        {
            WriteMarker("CleanExit", exitCode);
            if (!string.IsNullOrWhiteSpace(sessionLogPath))
            {
                AppendThrough(
                    sessionLogPath,
                    CreateLogEntry(
                        "INFORMATION",
                        "CogntApp.Diagnostics.CrashSafeLog",
                        $"Crash-safe session marker completed. ExitCode={exitCode}"));
            }
        }
    }

    internal static void RecordFatal(string message, Exception exception, string? logFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(exception);

        RecordFatal(message, exception.ToString(), logFilePath);
    }

    internal static void RecordFatal(string message, object? failure, string? logFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string details = failure?.ToString() ?? "(null)";
        RecordFatal(message, details, logFilePath);
    }

    private static void RecordFatal(string message, string details, string? logFilePath)
    {
        lock (SyncRoot)
        {
            string entry = CreateLogEntry(
                "CRITICAL",
                "CogntApp.Diagnostics.CrashSafeLog",
                message
                + Environment.NewLine
                + $"ProcessId={Environment.ProcessId}"
                + Environment.NewLine
                + details);
            string fatalPath = CreateFatalLogFilePath();
            AppendThrough(fatalPath, entry);

            string? targetLogPath = string.IsNullOrWhiteSpace(logFilePath)
                ? sessionLogPath
                : logFilePath;
            if (!string.IsNullOrWhiteSpace(targetLogPath))
            {
                AppendThrough(targetLogPath, entry);
            }

            WriteMarker("FatalObserved", exitCode: null);
        }
    }

    private static string CreateFatalLogFilePath()
    {
        Directory.CreateDirectory(AppDiagnosticPaths.LogDirectory);
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        return Path.Combine(AppDiagnosticPaths.LogDirectory, $"cognt-fatal-{timestamp}.log");
    }

    private static string CreateLogEntry(string level, string category, string message)
    {
        string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        return $"{timestamp} {level.PadRight(11)} {category} - {message}{Environment.NewLine}";
    }

    private static void WriteMarker(string status, int? exitCode)
    {
        string text = string.Join(
            Environment.NewLine,
            $"Status={status}",
            $"ProcessId={Environment.ProcessId}",
            $"ProcessName={Process.GetCurrentProcess().ProcessName}",
            $"UpdatedAt={DateTimeOffset.Now:O}",
            $"ExitCode={(exitCode.HasValue ? exitCode.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}",
            $"LogFile={sessionLogPath ?? string.Empty}")
            + Environment.NewLine;
        WriteThrough(MarkerPath, text);
    }

    private static string TryReadMarkerValue(string marker, string key)
    {
        string prefix = key + "=";
        using StringReader reader = new(marker);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return string.Empty;
    }

    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void AppendThrough(string path, string text)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = new(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            using StreamWriter writer = new(stream, Encoding.UTF8);
            writer.Write(text);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        catch
        {
        }
    }

    private static void WriteThrough(string path, string text)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = new(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            using StreamWriter writer = new(stream, Encoding.UTF8);
            writer.Write(text);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        catch
        {
        }
    }
}
