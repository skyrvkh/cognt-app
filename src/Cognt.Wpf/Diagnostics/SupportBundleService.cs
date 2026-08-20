using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CogntApp.Diagnostics;

/// <summary>
/// Severity of a single diagnostic event. Stable; new entries
/// appended to the end so persisted event logs are never re-mapped.
/// </summary>
public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3,
}

/// <summary>
/// One row in the bounded diagnostic event log.
/// </summary>
/// <param name="TimestampUtc">UTC timestamp the event was recorded.</param>
/// <param name="Severity">Closed taxonomy of severity.</param>
/// <param name="Code">Single-line stable event code.</param>
/// <param name="Payload">Bounded text payload (redacted; see <see cref="SupportBundleService.RedactPrivateContent"/>).</param>
public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    DiagnosticSeverity Severity,
    string Code,
    string Payload);

/// <summary>
/// Local diagnostic-event recorder + support-bundle writer. The
/// service is local-first, bounded, allowlisted, redacted, and
/// user-created. No telemetry, no automatic upload, no shell
/// commands, no log of note / card / clipboard / page content.
/// </summary>
public sealed class SupportBundleService
{
    /// <summary>Maximum number of events retained in the in-memory ring.</summary>
    public const int MaxEventCount = 256;

    /// <summary>Maximum payload length per event.</summary>
    public const int MaxPayloadLength = 1024;

    private readonly LinkedList<DiagnosticEvent> events = new();
    private readonly object gate = new();

    private static readonly Regex PathRegex = new(
        @"[A-Za-z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]*",
        RegexOptions.Compiled);

    private static readonly Regex Hex64Regex = new(
        @"\b[A-Fa-f0-9]{64}\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Records a diagnostic event. Trims the payload to
    /// <see cref="MaxPayloadLength"/> and trims the ring to
    /// <see cref="MaxEventCount"/> entries. Never throws.
    /// </summary>
    public void RecordEvent(DiagnosticSeverity severity, string code, string payload)
    {
        if (string.IsNullOrEmpty(code)) code = "unspecified";
        if (payload is null) payload = string.Empty;
        if (payload.Length > MaxPayloadLength)
        {
            payload = payload.Substring(0, MaxPayloadLength);
        }
        var entry = new DiagnosticEvent(
            TimestampUtc: DateTimeOffset.UtcNow,
            Severity: severity,
            Code: code,
            Payload: payload);
        lock (gate)
        {
            events.AddFirst(entry);
            while (events.Count > MaxEventCount) events.RemoveLast();
        }
    }

    /// <summary>Returns a snapshot of the in-memory event ring.</summary>
    public IReadOnlyList<DiagnosticEvent> SnapshotEvents()
    {
        lock (gate)
        {
            return events.ToList();
        }
    }

    /// <summary>
    /// Writes a support bundle to <paramref name="targetDir"/>.
    /// Only entries in <paramref name="allowlistedFiles"/> are
    /// copied; everything else is ignored. The bundle includes
    /// a single <c>diagnostics.log</c> summarising the in-memory
    /// event ring. Returns the path to the bundle directory.
    /// </summary>
    public string WriteBundle(string targetDir, IEnumerable<string> allowlistedFiles)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            throw new ArgumentException("Target directory must be supplied.", nameof(targetDir));
        }
        if (allowlistedFiles is null)
        {
            allowlistedFiles = Array.Empty<string>();
        }

        string resolvedTarget = Path.GetFullPath(targetDir);
        Directory.CreateDirectory(resolvedTarget);

        // Write the diagnostic log.
        string logPath = Path.Combine(resolvedTarget, "diagnostics.log");
        var sb = new StringBuilder();
        lock (gate)
        {
            foreach (var ev in events)
            {
                sb.Append(ev.TimestampUtc.ToString("O"))
                  .Append('\t')
                  .Append(ev.Severity)
                  .Append('\t')
                  .Append(ev.Code)
                  .Append('\t')
                  .Append(ev.Payload)
                  .Append('\n');
            }
        }
        File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);

        // Copy each allowlisted file. The source path is
        // normalized, but the target is placed under a flat
        // "files/" subdirectory with a sanitized name.
        string filesDir = Path.Combine(resolvedTarget, "files");
        Directory.CreateDirectory(filesDir);
        foreach (var source in allowlistedFiles)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) continue;
            string sanitized = SanitizeFileName(Path.GetFileName(source));
            if (string.IsNullOrEmpty(sanitized)) sanitized = "file-" + Guid.NewGuid().ToString("N");
            File.Copy(source, Path.Combine(filesDir, sanitized), overwrite: true);
        }

        return resolvedTarget;
    }

    /// <summary>
    /// Redacts private content from the supplied text. The
    /// implementation replaces:
    /// - Windows-style absolute paths with a stable
    ///   <c>&lt;path:&gt;</c> placeholder so diagnostic logs do
    ///   not contain the host file system layout.
    /// - 64-hex-char strings (typical of SHA-256 hashes) with a
    ///   stable <c>&lt;hash64&gt;</c> placeholder.
    /// </summary>
    public static string RedactPrivateContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? string.Empty;
        var result = PathRegex.Replace(content, "<path:>");
        result = Hex64Regex.Replace(result, "<hash64>");
        return result;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        return sb.ToString();
    }
}
