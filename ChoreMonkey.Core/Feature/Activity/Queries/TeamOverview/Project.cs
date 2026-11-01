using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Activity.Queries.TeamOverview;

/// <summary>The Team overview (admin or member PIN; null when there is no household or the PIN is wrong), folded from the events by the slice itself.</summary>
public static class TeamOverviewView
{
    public static TeamOverviewResponse? Project(IReadOnlyList<object> history, int pinCode, DateTime now)
    {
        // Verify household access (admin or member PIN)
        var householdCreated = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null) return null;

        var adminPinHash = history.OfType<AdminPinChanged>()
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefault()?.NewPinHash ?? householdCreated.PinHash;

        var isAdmin = PinHasher.VerifyPin(pinCode, adminPinHash);
        if (!isAdmin)
        {
            var memberPinHash = history.OfType<MemberPinChanged>()
                .LastOrDefault()?.NewMemberPinHash ?? householdCreated.MemberPinHash;
            var isMember = memberPinHash != null && PinHasher.VerifyPin(pinCode, memberPinHash);
            if (!isMember) return null;
        }

        var today = now.Date;

        // Current members, folded the way the Member Lookup view does
        var members = MembersOf(history);

        // Get deleted chore IDs
        var deletedChoreIds = history.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();

        // Get all chores (excluding deleted)
        var chores = history.OfType<ChoreCreated>()
            .Where(e => !deletedChoreIds.Contains(e.ChoreId))
            .ToDictionary(e => e.ChoreId);
        foreach (var update in history.OfType<ChoreUpdated>())
            if (chores.ContainsKey(update.ChoreId))
                chores[update.ChoreId] = chores[update.ChoreId] with
                {
                    DisplayName = update.DisplayName, Description = update.Description,
                    Frequency = update.Frequency, IsOptional = update.IsOptional,
                    StartDate = update.StartDate, IsRequired = update.IsRequired,
                    MissedDeduction = update.MissedDeduction,
                };

        // Get latest assignments
        var assignments = history.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Active pause windows per chore — a missed instance within a window isn't overdue.
        var chorePauses = ChorePauses.Build(history);

        // Get completions grouped by chore and member
        var completions = history.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.CompletedAt).First());

        var result = new List<MemberOverviewDto>();

        foreach (var (memberId, member) in members)
        {
            var choreStatuses = new List<ChoreStatusDto>();

            foreach (var (choreId, chore) in chores)
            {
                // Check if this member is assigned to this chore
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);

                if (!isAssigned) continue;

                // Get this member's last completion
                var lastCompletion = completions.GetValueOrDefault((choreId, memberId));
                var lastCompletedAt = lastCompletion?.CompletedAt;

                // Get chore start date
                var choreStartDate = chore.StartDate?.Date
                    ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);

                // Calculate status
                var (status, overduePeriod) = CalculateStatus(
                    chore.Frequency,
                    lastCompletedAt,
                    today,
                    choreStartDate,
                    chore.IsOptional,
                    chorePauses.GetValueOrDefault(choreId));

                choreStatuses.Add(new ChoreStatusDto(
                    choreId,
                    chore.DisplayName,
                    status,
                    overduePeriod,
                    lastCompletedAt,
                    chore.IsOptional));
            }

            // Sort: overdue first, then pending, then completed
            var sortedChores = choreStatuses
                .OrderBy(c => c.Status switch { "overdue" => 0, "pending" => 1, "completed" => 2, _ => 3 })
                .ThenBy(c => c.DisplayName)
                .ToList();

            result.Add(new MemberOverviewDto(
                memberId,
                member.Nickname,
                sortedChores.Count,
                sortedChores.Count(c => c.Status == "completed"),
                sortedChores.Count(c => c.Status == "overdue"),
                sortedChores));
        }

        // Sort: members with overdue first, then by total chores
        return new TeamOverviewResponse(
            result.OrderByDescending(m => m.OverdueCount > 0)
                  .ThenByDescending(m => m.OverdueCount)
                  .ThenByDescending(m => m.TotalChores)
                  .ToList());
    }

    /// <summary>The household's current members, folded the way the Member Lookup view does.</summary>
    private static Dictionary<Guid, MemberDto> MembersOf(IReadOnlyList<object> events)
    {
        var members = new Dictionary<Guid, MemberDto>();
        foreach (var e in events)
        {
            switch (e)
            {
                case MemberJoinedHousehold joined:
                    members[joined.MemberId] = new(joined.MemberId, joined.Nickname, null);
                    break;
                case MemberNicknameChanged changed:
                    if (members.TryGetValue(changed.MemberId, out var m1))
                        members[changed.MemberId] = m1 with { Nickname = changed.NewNickname };
                    break;
                case MemberStatusChanged status:
                    if (members.TryGetValue(status.MemberId, out var m2))
                        members[status.MemberId] = m2 with { Status = status.Status };
                    break;
                case MemberRemoved removed:
                    members.Remove(removed.MemberId);
                    break;
            }
        }
        return members;
    }

    private static (string Status, string? OverduePeriod) CalculateStatus(
        ChoreFrequency? frequency,
        DateTime? lastCompleted,
        DateTime today,
        DateTime choreStartDate,
        bool isOptional,
        List<PauseWindow>? pauses)
    {
        if (frequency == null || frequency.Type.ToLower() == "once")
        {
            // One-time chore: completed if ever done
            return lastCompleted != null ? ("completed", null) : ("pending", null);
        }

        // Check if completed this period
        var completedThisPeriod = IsCompletedThisPeriod(frequency, lastCompleted, today);
        if (completedThisPeriod)
        {
            return ("completed", null);
        }

        // Optional chores can't be overdue
        if (isOptional)
        {
            return ("pending", null);
        }

        // Check if overdue
        var overduePeriod = CalculateOverduePeriod(frequency, lastCompleted, today, choreStartDate, pauses);
        if (overduePeriod != null)
        {
            return ("overdue", overduePeriod);
        }

        return ("pending", null);
    }

    private static bool IsCompletedThisPeriod(ChoreFrequency frequency, DateTime? lastCompleted, DateTime today)
    {
        if (lastCompleted == null) return false;

        return frequency.Type.ToLower() switch
        {
            "daily" => lastCompleted.Value.Date >= today,
            "weekly" => lastCompleted.Value.Date >= GetMondayOfWeek(today),
            "interval" => lastCompleted.Value.Date >= today.AddDays(-(frequency.IntervalDays ?? 1) + 1),
            _ => false
        };
    }

    private static string? CalculateOverduePeriod(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt, List<PauseWindow>? pauses)
    {
        if (frequency == null) return null;

        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today, choreCreatedAt, pauses),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today, choreCreatedAt, pauses),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today, choreCreatedAt, pauses),
            _ => null
        };
    }

    private static string? CalculateDailyOverdue(DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt, List<PauseWindow>? pauses)
    {
        var yesterday = today.AddDays(-1);

        if (choreCreatedAt > yesterday) return null;
        if (lastCompleted?.Date >= today) return null;
        if (lastCompleted?.Date >= yesterday) return null;
        if (pauses.CoversDate(yesterday)) return null;

        return "yesterday";
    }

    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string? CalculateWeeklyOverdue(string[]? days, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt, List<PauseWindow>? pauses)
    {
        var currentWeekStart = GetMondayOfWeek(today);
        var previousWeekStart = currentWeekStart.AddDays(-7);

        if (days == null || days.Length == 0)
        {
            if (choreCreatedAt >= previousWeekStart) return null;
            if (lastCompleted?.Date >= currentWeekStart) return null;
            if (lastCompleted?.Date >= previousWeekStart) return null;
            if (pauses.CoversAnyInRange(previousWeekStart, currentWeekStart.AddDays(-1))) return null;
            return "last week";
        }

        var requiredDays = days
            .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
            .ToHashSet();

        for (int i = 0; i < 7; i++)
        {
            var checkDate = previousWeekStart.AddDays(i);

            if (!requiredDays.Contains(checkDate.DayOfWeek)) continue;
            if (choreCreatedAt > checkDate) continue;
            if (lastCompleted?.Date >= checkDate) return null;
            if (pauses.CoversDate(checkDate)) continue;

            return $"last {checkDate.DayOfWeek}";
        }

        return null;
    }

    private static string? CalculateIntervalOverdue(int intervalDays, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt, List<PauseWindow>? pauses)
    {
        DateTime dueDate;

        if (lastCompleted == null)
        {
            dueDate = choreCreatedAt.AddDays(intervalDays);
        }
        else
        {
            dueDate = lastCompleted.Value.Date.AddDays(intervalDays);
        }

        if (today <= dueDate) return null;
        if (pauses.CoversDate(dueDate)) return null;

        var daysOverdue = (int)(today - dueDate).TotalDays;

        if (daysOverdue > intervalDays) return null;

        if (daysOverdue == 1) return "yesterday";
        return $"{daysOverdue} days ago";
    }
}
