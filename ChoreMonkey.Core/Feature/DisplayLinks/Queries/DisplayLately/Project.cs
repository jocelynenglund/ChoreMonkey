using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLately;

public record DisplayDay(string Day, int Count);
public record DisplayPerson(string Name, List<DisplayDay> Days, int Total);
public record DisplayLatelyResponse(string HouseholdName, string From, string To, List<DisplayPerson> People);

/// <summary>
/// The Display Lately view: for each active member, how many chores they completed on each of the last 40 days
/// (today included, oldest first). Busiest first, then by name. "Now" is the household's local time (Stockholm).
/// No pay or money is ever in it.
/// </summary>
public static class DisplayLatelyView
{
    public const int Days = 40;

    public static DisplayLatelyResponse? Project(IReadOnlyList<object> history, DateTime now)
    {
        var created = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (created == null) return null;

        var today = now.Date;
        var first = today.AddDays(-(Days - 1));
        var members = DisplayMembers.Active(history);

        var counts = history.OfType<ChoreCompleted>()
            .Where(c => DisplayClock.LocalDay(c.CompletedAt) >= first && DisplayClock.LocalDay(c.CompletedAt) <= today)
            .GroupBy(c => (c.CompletedByMemberId, DisplayClock.LocalDay(c.CompletedAt)))
            .ToDictionary(g => g.Key, g => g.Count());

        var people = members
            .Select(m =>
            {
                var days = Enumerable.Range(0, Days)
                    .Select(i => first.AddDays(i))
                    .Select(d => new DisplayDay(d.ToString("yyyy-MM-dd"), counts.GetValueOrDefault((m.MemberId, d))))
                    .ToList();
                return new DisplayPerson(m.Nickname, days, days.Sum(d => d.Count));
            })
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new DisplayLatelyResponse(created.Name, first.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"), people);
    }
}
