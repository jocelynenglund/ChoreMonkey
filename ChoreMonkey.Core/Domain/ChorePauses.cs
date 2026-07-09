using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Domain;

/// <summary>
/// A resolved pause window for a chore. Dates are compared inclusively at
/// day granularity — a chore is paused on any date from Start through End.
/// </summary>
public record PauseWindow(Guid PauseId, DateTime Start, DateTime End, string? Reason = null)
{
    public bool Covers(DateTime date) => date.Date >= Start.Date && date.Date <= End.Date;
}

/// <summary>
/// Builds the set of active pause windows per chore from the chore event stream,
/// honoring <see cref="ChorePauseRemoved"/>, and exposes the checks used by the
/// overdue/penalty calculators. A missed instance whose reference date falls in
/// any active window is exempt (no overdue display, no salary deduction).
/// </summary>
public static class ChorePauses
{
    /// <summary>choreId → active (non-removed) pause windows.</summary>
    public static Dictionary<Guid, List<PauseWindow>> Build(IEnumerable<object> choreEvents)
    {
        var events = choreEvents.ToList();

        var removed = events.OfType<ChorePauseRemoved>()
            .Select(e => e.PauseId)
            .ToHashSet();

        return events.OfType<ChorePaused>()
            .Where(e => !removed.Contains(e.PauseId))
            .GroupBy(e => e.ChoreId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => new PauseWindow(e.PauseId, e.PauseStart, e.PauseEnd, e.Reason)).ToList());
    }

    /// <summary>True when <paramref name="date"/> falls within any window.</summary>
    public static bool CoversDate(this List<PauseWindow>? pauses, DateTime date)
        => pauses != null && pauses.Any(w => w.Covers(date));

    /// <summary>True when any date in [start, end] (inclusive) falls within any window.</summary>
    public static bool CoversAnyInRange(this List<PauseWindow>? pauses, DateTime startInclusive, DateTime endInclusive)
        => pauses != null && pauses.Any(w => w.Start.Date <= endInclusive.Date && w.End.Date >= startInclusive.Date);
}
