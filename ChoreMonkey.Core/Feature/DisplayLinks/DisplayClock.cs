namespace ChoreMonkey.Core.Feature.DisplayLinks;

/// <summary>The household's day is the Stockholm day. Views take "now" as a Stockholm wall-clock time; the adapter makes it here.</summary>
public static class DisplayClock
{
    static readonly TimeZoneInfo Stockholm = Find();

    static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Europe/Stockholm", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>Stockholm wall-clock time for a UTC instant.</summary>
    public static DateTime LocalNow(DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Stockholm);

    /// <summary>The Stockholm day a completion happened on. A completion stamped in UTC is converted; one without a zone is taken as already local.</summary>
    public static DateTime LocalDay(DateTime completedAt) =>
        completedAt.Kind == DateTimeKind.Utc ? LocalNow(completedAt).Date : completedAt.Date;
}
