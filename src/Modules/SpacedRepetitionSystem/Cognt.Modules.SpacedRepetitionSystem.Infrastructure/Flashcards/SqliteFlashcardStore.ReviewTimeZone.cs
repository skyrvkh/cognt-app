namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private static readonly TimeZoneInfo ReviewTimeZone = ResolveReviewTimeZone();

    private static (DateTimeOffset TodayStartUtc, DateTimeOffset TomorrowStartUtc) GetLocalTodayUtcBounds(
        DateTimeOffset nowUtc)
    {
        DateTimeOffset nowLocal = TimeZoneInfo.ConvertTime(nowUtc, ReviewTimeZone);
        DateTime localToday = nowLocal.Date;
        DateTimeOffset todayStartLocal = ToReviewTimeZoneOffset(localToday);
        DateTimeOffset tomorrowStartLocal = ToReviewTimeZoneOffset(localToday.AddDays(1));
        return (todayStartLocal.ToUniversalTime(), tomorrowStartLocal.ToUniversalTime());
    }

    private static DateTimeOffset ToReviewTimeZoneOffset(DateTime localDateTime)
    {
        DateTime unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, ReviewTimeZone.GetUtcOffset(unspecified));
    }

    private static TimeZoneInfo ResolveReviewTimeZone()
    {
        if (TryFindTimeZone("Asia/Manila", out TimeZoneInfo timeZone))
        {
            return timeZone;
        }

        if (TryFindTimeZone("Singapore Standard Time", out timeZone))
        {
            return timeZone;
        }

        return TimeZoneInfo.Local;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }
}
