using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Chores.Queries.DisconnectionNotice;

/// <summary>The DisconnectionNotice view, folded from the events by the slice itself. The handler is only the store adapter.</summary>
public static class DisconnectionNoticeView
{
    public const int WindowMonths = 2;

    public static GetDisconnectionNoticeResponse? Project(IReadOnlyList<object> history, Guid memberId, DateTime now)
    {
        // History is the household's and the chores' events together; each type lives in one stream.
        var householdEvents = history;
        var choreEvents = history;

        var memberExists = householdEvents.OfType<MemberJoinedHousehold>()
            .Any(e => e.MemberId == memberId);
        if (!memberExists) return null;

        var today = now.Date;
        var windowStart = today.AddMonths(-WindowMonths);

        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        foreach (var update in choreEvents.OfType<ChoreUpdated>())
            if (chores.ContainsKey(update.ChoreId))
                chores[update.ChoreId] = chores[update.ChoreId] with
                {
                    DisplayName = update.DisplayName, Description = update.Description,
                    Frequency = update.Frequency, IsOptional = update.IsOptional,
                    StartDate = update.StartDate, IsRequired = update.IsRequired,
                    MissedDeduction = update.MissedDeduction,
                };

        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        var chorePauses = ChorePauses.Build(choreEvents);

        var completionsByChore = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var missed = new List<DisconnectedChoreDto>();
        var anyCompletedInWindow = false;
        var anyOlderThanWindow = false;

        foreach (var (choreId, chore) in chores)
        {
            if (chore.IsOptional) continue;

            var type = chore.Frequency?.Type?.ToLower();
            if (type is null or "once") continue;

            var assignment = assignments.GetValueOrDefault(choreId);
            var isShared = assignment?.AssignToAll == true;
            var isAssignedToMe = isShared ||
                (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
            if (!isAssignedToMe) continue;

            // Excused: any pause overlapping the window takes the chore out of the reckoning.
            if (chorePauses.GetValueOrDefault(choreId).CoversAnyInRange(windowStart, today))
                continue;

            // For shared chores, anyone in the household doing it counts.
            var completions = (completionsByChore.GetValueOrDefault(choreId) ?? [])
                .Where(c => isShared || c.CompletedByMemberId == memberId)
                .ToList();

            if (completions.Any(c => c.CompletedAt.Date >= windowStart))
            {
                anyCompletedInWindow = true;
                continue;
            }

            var choreStartDate = chore.StartDate?.Date
                ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);
            if (choreStartDate <= windowStart)
                anyOlderThanWindow = true;

            missed.Add(new DisconnectedChoreDto(
                choreId,
                chore.DisplayName,
                chore.Frequency?.Type,
                completions.MaxBy(c => c.CompletedAt)?.CompletedAt));
        }

        // Only cut off when there's been a full two-month stretch with nothing done.
        var isDisconnected = !anyCompletedInWindow && anyOlderThanWindow;

        return new GetDisconnectionNoticeResponse(
            isDisconnected,
            windowStart,
            isDisconnected ? missed.OrderBy(c => c.DisplayName).ToList() : []);
    }
}

