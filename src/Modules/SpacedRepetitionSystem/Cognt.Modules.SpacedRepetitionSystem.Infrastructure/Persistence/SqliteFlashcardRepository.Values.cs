using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static void AddScheduleParameters(SqliteCommand command, FlashcardSchedule schedule)
    {
        command.Parameters.AddWithValue("$fsrsCardStateJson", schedule.SerializedState);
        command.Parameters.AddWithValue("$dueAt", schedule.DueUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$state", (int)schedule.State);
        command.Parameters.AddWithValue("$stability", DbValue(schedule.Stability));
        command.Parameters.AddWithValue("$difficulty", DbValue(schedule.Difficulty));
        command.Parameters.AddWithValue("$fsrsStep", DbValue(schedule.Step));
        command.Parameters.AddWithValue("$lastReviewAt", DbValue(schedule.LastReviewUtc?.ToUnixTimeSeconds()));
        command.Parameters.AddWithValue("$elapsedDays", Math.Max(0, schedule.ElapsedDays));
        command.Parameters.AddWithValue("$scheduledDays", Math.Max(0, schedule.ScheduledDays));
        command.Parameters.AddWithValue("$reps", Math.Max(0, schedule.Reps));
        command.Parameters.AddWithValue("$lapses", Math.Max(0, schedule.Lapses));
    }

    private static string NormalizeName(string name)
    {
        string normalized = name.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : normalized;
    }

    private static string NormalizeRelPath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("Path is required.", nameof(path))
            : normalized;
    }

    private static string? NormalizeOptionalRelPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return NormalizeRelPath(path);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static object DbValue(double? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(long? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static long NowUnixTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
