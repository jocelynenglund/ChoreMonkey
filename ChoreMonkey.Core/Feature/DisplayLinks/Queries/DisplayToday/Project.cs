using ChoreMonkey.Core.Feature.Chores.Queries.MyChores;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayToday;

/// <summary>state is "open" or "done"; this app has no "claimed". By is set when the chore is done.</summary>
public record DisplayChore(Guid ChoreId, string Title, string State, string? By);

public record DisplayTodayResponse(string HouseholdName, string Day, string HouseholdUrl, List<DisplayChore> Chores);

/// <summary>
/// The Display Today view: what the household's own screens put on today's list, for a connected screen. It is each
/// active member's "my chores" (what is still to do this period is open; what was done today is done), merged by
/// chore: a chore for several people is listed once, done when anyone did it today. A chore nobody has, or that was
/// deleted, is not listed. "Now" is the household's local time (Stockholm). No pay or money is ever in it.
/// </summary>
public static class DisplayTodayView
{
    public static DisplayTodayResponse? Project(IReadOnlyList<object> history, DateTime now, string householdBaseUrl)
    {
        var created = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (created == null) return null;

        var members = DisplayMembers.Active(history);
        var byChore = new Dictionary<Guid, DisplayChore>();

        foreach (var (memberId, nickname) in members)
        {
            var mine = MyChoresView.Project(history, memberId, now);
            if (mine == null) continue;

            foreach (var open in mine.Pending)
                byChore.TryAdd(open.ChoreId, new DisplayChore(open.ChoreId, open.DisplayName, "open", null));

            foreach (var done in mine.Completed.Where(c => DisplayClock.LocalDay(c.CompletedAt) == now.Date))
                byChore[done.ChoreId] = new DisplayChore(done.ChoreId, done.DisplayName, "done", done.CompletedByName ?? nickname);
        }

        var slug = history.OfType<HouseholdSlugSet>().LastOrDefault()?.Slug;
        var url = string.IsNullOrEmpty(slug)
            ? $"{householdBaseUrl.TrimEnd('/')}/household/{created.HouseholdId}"
            : $"{householdBaseUrl.TrimEnd('/')}/h/{slug}";

        var chores = byChore.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new DisplayTodayResponse(created.Name, now.ToString("yyyy-MM-dd"), url, chores);
    }
}
