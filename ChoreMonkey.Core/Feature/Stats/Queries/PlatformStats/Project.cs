using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Stats.Queries.PlatformStats;

/// <summary>
/// The platform stats, folded from each household's events by the slice itself. The view spans several streams,
/// so it takes one history per household stream (its household events, with its chore events appended).
/// A stream without a HouseholdCreated is not a household and is skipped.
/// </summary>
public static class PlatformStatsView
{
    public static PlatformStatsResponse Project(
        IReadOnlyList<IReadOnlyList<object>> households,
        string? dataPath,
        bool? dataDirectoryExists,
        DateTime now)
    {
        var rows = new List<HouseholdStatsDto>();
        var totalMembers = 0;
        var totalChores = 0;

        foreach (var history in households)
        {
            // Get household created event
            var created = history.OfType<HouseholdCreated>().FirstOrDefault();
            if (created == null) continue;

            // Count members (joined - removed)
            var memberJoins = history.OfType<MemberJoinedHousehold>()
                .Select(e => e.MemberId)
                .ToHashSet();
            var memberRemovals = history.OfType<MemberRemoved>()
                .Select(e => e.MemberId)
                .ToHashSet();
            var memberCount = memberJoins.Except(memberRemovals).Count();

            // Count chores (created - deleted)
            var choreCreations = history.OfType<ChoreCreated>()
                .Select(e => e.ChoreId)
                .ToHashSet();
            var choreDeletions = history.OfType<ChoreDeleted>()
                .Select(e => e.ChoreId)
                .ToHashSet();
            var choreCount = choreCreations.Except(choreDeletions).Count();

            // Parse created timestamp
            var createdAt = DateTime.TryParse(created.TimestampUtc, out var parsed)
                ? parsed
                : now;

            rows.Add(new HouseholdStatsDto(
                created.HouseholdId,
                created.Name,
                memberCount,
                choreCount,
                createdAt));

            totalMembers += memberCount;
            totalChores += choreCount;
        }

        return new PlatformStatsResponse(
            rows.Count,
            totalMembers,
            totalChores,
            rows.OrderByDescending(h => h.CreatedAt).ToList(),
            dataPath,
            dataDirectoryExists);
    }
}
