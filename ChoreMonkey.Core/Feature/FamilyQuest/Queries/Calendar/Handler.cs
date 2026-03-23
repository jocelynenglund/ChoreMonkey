using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.RegularExpressions;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Calendar;

public record GetCalendarQuery(string IcsUrl);

public record CalendarEventDto(
    string Summary,
    string? Time,   // "14:30" or null if all-day
    bool AllDay,
    DateTime Date);

public record GetCalendarResponse(
    List<CalendarEventDto> Today,
    List<CalendarEventDto> Week);

internal class Handler(HttpClient http)
{
    public async Task<GetCalendarResponse> HandleAsync(GetCalendarQuery request)
    {
        // Only allow Google Calendar ICS URLs
        if (!IsAllowedUrl(request.IcsUrl))
            throw new ArgumentException("Only Google Calendar ICS URLs are allowed.");

        var ics  = await http.GetStringAsync(request.IcsUrl);
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

    private static List<CalendarEventDto> ParseICS(string text)
    {
        var events = new List<CalendarEventDto>();
        var lines  = UnfoldLines(text);

        string? summary  = null;
        DateTime? start  = null;
        bool allDay      = false;

        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT") { summary = null; start = null; allDay = false; }
            else if (line == "END:VEVENT")
            {
                if (summary != null && start.HasValue)
                    events.Add(new CalendarEventDto(
                        summary,
                        allDay ? null : start.Value.ToString("HH:mm"),
                        allDay,
                        start.Value));
            }
            else if (line.StartsWith("SUMMARY:"))
                summary = line[8..].Trim();
            else if (line.StartsWith("DTSTART;VALUE=DATE:"))
                { start = ParseDate(line[19..]); allDay = true; }
            else if (line.StartsWith("DTSTART:"))
                start = ParseDateTime(line[8..]);
            else if (line.StartsWith("DTSTART;TZID="))
                start = ParseDateTime(line[(line.IndexOf(':') + 1)..]);
        }

        return events;
    }

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
