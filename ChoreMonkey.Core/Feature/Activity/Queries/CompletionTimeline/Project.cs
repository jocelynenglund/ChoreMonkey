using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Activity.Queries.CompletionTimeline;

/// <summary>The completion timeline (the last 30 days of household activity), folded from the events by the slice itself.</summary>
public static class CompletionTimelineView
{
    public static GetCompletionTimelineResponse Project(IReadOnlyList<object> history, DateTime now)
    {
        var cutoff = now.AddDays(-30);

        var choreNames = history
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        var memberNicknames = history
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(m => m.MemberId, m => m.Nickname);

        // Apply nickname changes
        foreach (var change in history.OfType<MemberNicknameChanged>())
        {
            memberNicknames[change.MemberId] = change.NewNickname;
        }

        var activities = new List<ActivityEntry>();

        // Chore completions
        foreach (var e in history.OfType<ChoreCompleted>())
        {
            var ts = e.CompletedAt;
            if (ts < cutoff) continue;

            var choreName = choreNames.GetValueOrDefault(e.ChoreId, "Unknown chore");
            var memberName = memberNicknames.GetValueOrDefault(e.CompletedByMemberId, "Unknown");

            activities.Add(new ActivityEntry(
                "completion",
                $"{memberName} completed {choreName}",
                ts,
                e.ChoreId,
                e.CompletedByMemberId
            ));
        }

        // Chore assignments
        foreach (var e in history.OfType<ChoreAssigned>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            var choreName = choreNames.GetValueOrDefault(e.ChoreId, "Unknown chore");
            string description;

            if (e.AssignToAll)
            {
                description = $"{choreName} assigned to everyone";
            }
            else if (e.AssignedToMemberIds?.Length > 0)
            {
                var names = e.AssignedToMemberIds
                    .Select(id => memberNicknames.GetValueOrDefault(id, "Unknown"))
                    .ToList();
                description = $"{choreName} assigned to {string.Join(", ", names)}";
            }
            else
            {
                description = $"{choreName} unassigned";
            }

            activities.Add(new ActivityEntry(
                "assignment",
                description,
                ts,
                e.ChoreId
            ));
        }

        // Nickname changes
        foreach (var e in history.OfType<MemberNicknameChanged>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            activities.Add(new ActivityEntry(
                "nickname_change",
                $"{e.OldNickname} changed name to {e.NewNickname}",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Status changes
        foreach (var e in history.OfType<MemberStatusChanged>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            var memberName = memberNicknames.GetValueOrDefault(e.MemberId, "Unknown");

            activities.Add(new ActivityEntry(
                "status_change",
                $"{memberName} updated status: \"{e.Status}\"",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Member joined
        foreach (var e in history.OfType<MemberJoinedHousehold>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            activities.Add(new ActivityEntry(
                "member_joined",
                $"{e.Nickname} joined the household",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Chore created
        foreach (var e in history.OfType<ChoreCreated>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            activities.Add(new ActivityEntry(
                "chore_created",
                $"New chore added: {e.DisplayName}",
                ts,
                e.ChoreId
            ));
        }

        // Member removed
        foreach (var e in history.OfType<MemberRemoved>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;

            activities.Add(new ActivityEntry(
                "member_removed",
                $"{e.Nickname} was removed from the household",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Sort by timestamp descending
        var result = activities
            .OrderByDescending(a => a.Timestamp)
            .ToList();

        return new GetCompletionTimelineResponse(result);
    }

    private static DateTimeOffset ParseTimestamp(string timestampUtc)
    {
        if (DateTimeOffset.TryParse(timestampUtc, out var ts))
            return ts;
        return DateTimeOffset.MinValue;
    }
}
