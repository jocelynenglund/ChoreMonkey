using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.RegularExpressions;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Calendar;

public record GetCalendarQuery(string IcsUrl);

public record CalendarEventDto(
    string Summary,
    string? Time,
    bool AllDay,
    DateTime Date);

public record GetCalendarResponse(
    List<CalendarEventDto> Today,
    List<CalendarEventDto> Week);

internal class Handler(HttpClient http)
{
    public async Task<GetCalendarResponse> HandleAsync(GetCalendarQuery request)
    {
        if (!IsAllowedUrl(request.IcsUrl))
            throw new ArgumentException("Only Google Calendar ICS URLs are allowed.");

        var ics    = await http.GetStringAsync(request.IcsUrl);
        var events = ParseICS(ics);

        var now       = DateTime.UtcNow.Date;
        var dow       = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
        var weekStart = now.AddDays(-dow);
        var weekEnd   = weekStart.AddDays(7);

        var today = events
            .Where(e => e.Date.Date == now)
            .OrderBy(e => e.Date)
            .ToList();

        var week = events
            .Where(e => e.Date.Date >= weekStart && e.Date.Date < weekEnd)
            .OrderBy(e => e.Date)
            .ToList();

        return new GetCalendarResponse(today, week);
    }

    private static bool IsAllowedUrl(string url) =>
        Regex.IsMatch(url, @"^https://calendar\.google\.com/calendar/ical/[^/]+/[^/]+/basic\.ics$");

    private record RawEvent(string Summary, DateTime Start, bool AllDay, string? Rrule, HashSet<DateTime> ExDates);

    private static List<CalendarEventDto> ParseICS(string text)
    {
        var rawEvents = new List<RawEvent>();
        var lines     = UnfoldLines(text).ToList();

        string?      summary  = null;
        DateTime?    start    = null;
        bool         allDay   = false;
        string?      rrule    = null;
        var          exDates  = new HashSet<DateTime>();

        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT")
            {
                summary = null; start = null; allDay = false; rrule = null; exDates = new();
            }
            else if (line == "END:VEVENT")
            {
                if (summary != null && start.HasValue)
                    rawEvents.Add(new RawEvent(summary, start.Value, allDay, rrule, exDates));
            }
            else if (line.StartsWith("SUMMARY:"))
                summary = line[8..].Trim();
            else if (line.StartsWith("DTSTART;VALUE=DATE:"))
                { start = ParseDate(line[19..]); allDay = true; }
            else if (line.StartsWith("DTSTART:"))
                start = ParseDateTime(line[8..]);
            else if (line.StartsWith("DTSTART;TZID="))
                start = ParseDateTime(line[(line.IndexOf(':') + 1)..]);
            else if (line.StartsWith("RRULE:"))
                rrule = line[6..].Trim();
            else if (line.StartsWith("EXDATE;VALUE=DATE:") || line.StartsWith("EXDATE:"))
            {
                var val = line[(line.IndexOf(':') + 1)..].Trim();
                foreach (var d in val.Split(','))
                    try { exDates.Add(d.Length >= 8 ? ParseDate(d[..8]) : ParseDateTime(d)); } catch { }
            }
        }

        // Window: today through end of this week (Mon-Sun)
        var now       = DateTime.UtcNow.Date;
        var dow       = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
        var weekStart = now.AddDays(-dow);
        var weekEnd   = weekStart.AddDays(7);

        var result = new List<CalendarEventDto>();

        foreach (var ev in rawEvents)
        {
            if (ev.Rrule is null)
            {
                // Single event — include if in window
                if (ev.Start.Date >= weekStart && ev.Start.Date < weekEnd)
                    result.Add(ToDto(ev, ev.Start));
            }
            else
            {
                // Expand recurring event within window
                var occurrences = ExpandRecurring(ev, weekStart, weekEnd);
                result.AddRange(occurrences.Select(occ => ToDto(ev, occ)));
            }
        }

        return result;
    }

    private static CalendarEventDto ToDto(RawEvent ev, DateTime occurrence) =>
        new(ev.Summary,
            ev.AllDay ? null : occurrence.ToString("HH:mm"),
            ev.AllDay,
            occurrence);

    private static IEnumerable<DateTime> ExpandRecurring(RawEvent ev, DateTime windowStart, DateTime windowEnd)
    {
        // Parse RRULE key-value pairs
        var parts = ev.Rrule!.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("FREQ", out var freq)) yield break;

        // UNTIL or COUNT limit
        DateTime? until = null;
        int? count = null;
        if (parts.TryGetValue("UNTIL", out var untilStr))
            try { until = untilStr.Length >= 8 ? ParseDate(untilStr[..8]) : ParseDateTime(untilStr); } catch { }
        if (parts.TryGetValue("COUNT", out var countStr) && int.TryParse(countStr, out var c))
            count = c;

        int interval = parts.TryGetValue("INTERVAL", out var iv) && int.TryParse(iv, out var i) ? i : 1;

        // BYDAY for weekly
        var byDay = parts.TryGetValue("BYDAY", out var bd)
            ? bd.Split(',').Select(ParseDayOfWeek).Where(d => d.HasValue).Select(d => d!.Value).ToHashSet()
            : null;

        var cursor    = ev.Start;
        int generated = 0;
        int maxIter   = 3000; // safety

        while (maxIter-- > 0)
        {
            if (until.HasValue && cursor.Date > until.Value.Date) break;
            if (count.HasValue && generated >= count.Value) break;
            if (cursor.Date > windowEnd) break;

            // For WEEKLY with BYDAY, iterate each matching day in the week
            if (freq.Equals("WEEKLY", StringComparison.OrdinalIgnoreCase) && byDay != null)
            {
                // Find start of the week containing cursor
                var weekMon = cursor.Date.AddDays(-(cursor.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)cursor.DayOfWeek - 1));
                for (int d = 0; d < 7; d++)
                {
                    var candidate = weekMon.AddDays(d);
                    if (!byDay.Contains(candidate.DayOfWeek)) continue;
                    // Keep original time
                    var occ = candidate.Add(ev.Start.TimeOfDay);
                    if (occ < ev.Start) continue;
                    if (ev.ExDates.Contains(occ.Date)) continue;
                    if (occ.Date >= windowStart && occ.Date < windowEnd)
                    {
                        yield return occ;
                        generated++;
                    }
                }
                cursor = cursor.AddDays(7 * interval);
            }
            else
            {
                // Simple: check cursor itself
                if (!ev.ExDates.Contains(cursor.Date) && cursor.Date >= windowStart && cursor.Date < windowEnd)
                {
                    yield return cursor;
                    generated++;
                }

                cursor = freq.ToUpperInvariant() switch
                {
                    "DAILY"   => cursor.AddDays(interval),
                    "WEEKLY"  => cursor.AddDays(7 * interval),
                    "MONTHLY" => cursor.AddMonths(interval),
                    "YEARLY"  => cursor.AddYears(interval),
                    _         => windowEnd // stop unknown
                };
            }

            // Advance cursor if we haven't moved past window yet (simple non-BYDAY weekly/daily/etc.)
            if (cursor.Date > windowEnd && freq.Equals("WEEKLY", StringComparison.OrdinalIgnoreCase) && byDay == null)
                break;
        }
    }

    private static DayOfWeek? ParseDayOfWeek(string s) => s.Trim().ToUpperInvariant() switch
    {
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        "SU" => DayOfWeek.Sunday,
        _    => null
    };

    private static IEnumerable<string> UnfoldLines(string text) =>
        text.Replace("\r\n ", "").Replace("\r\n\t", "")
            .Replace("\r\n", "\n").Split('\n');

    private static DateTime ParseDate(string s) =>
        new(int.Parse(s[..4]), int.Parse(s[4..6]), int.Parse(s[6..8]));

    private static DateTime ParseDateTime(string s)
    {
        if (s.Length < 15) return ParseDate(s);
        var dt = new DateTime(
            int.Parse(s[..4]), int.Parse(s[4..6]), int.Parse(s[6..8]),
            int.Parse(s[9..11]), int.Parse(s[11..13]), int.Parse(s[13..15]),
            s.EndsWith('Z') ? DateTimeKind.Utc : DateTimeKind.Local);
        return dt.ToUniversalTime();
    }
}

internal static class CalendarEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/calendar",
            async (Guid householdId, string url, Handler handler) =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return Results.BadRequest("url parameter required");
                try
                {
                    var result = await handler.HandleAsync(new GetCalendarQuery(url));
                    return Results.Ok(result);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });
    }
}
