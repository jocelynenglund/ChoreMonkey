using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Chores.Queries.OverdueChores;

/// <summary>The Overdue Chores view (admin only: null when there is no household or the PIN is wrong), folded from the events by the slice itself.</summary>
public static class OverdueChoresView
{
    public static GetOverdueResponse? Project(IReadOnlyList<object> history, int pinCode, DateTime now)
    {
        // History is the household's and the chores' events together; each type lives in one stream.
        var householdEvents = history;
        var choreEvents = history;
        
        // Verify admin access
        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null) return null;
        
        var adminPinHash = householdEvents.OfType<AdminPinChanged>()
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        
        if (!PinHasher.VerifyPin(pinCode, adminPinHash))
        {
            return null; // Not admin - return null (endpoint will return 403)
        }
        
        var today = now.Date;
        
        // Get members via MemberLookup query
        var members = MembersOf(householdEvents);
        
        // Get deleted chore IDs
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
        // Get all chores with frequencies (excluding deleted)
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(e => !deletedChoreIds.Contains(e.ChoreId))
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
        
        // Get latest assignments
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Active pause windows per chore — a missed instance within a window isn't overdue.
        var chorePauses = ChorePauses.Build(choreEvents);

        // Get completions grouped by chore and member
        var completions = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(
                g => g.Key, 
                g => g.OrderByDescending(c => c.CompletedAt).First());
        
        var result = new List<MemberOverdueDto>();
        
        foreach (var (memberId, member) in members)
        {
            var overdueChores = new List<OverdueChoreDto>();
            
            foreach (var (choreId, chore) in chores)
            {
                // Skip optional/bonus chores - they can never be overdue
                if (chore.IsOptional) continue;
                
                // Check if this member is assigned to this chore
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
                
                if (!isAssigned) continue;
                
                // Get this member's last completion
                var lastCompletion = completions.GetValueOrDefault((choreId, memberId));
                var lastCompletedAt = lastCompletion?.CompletedAt;
                
                // Get chore start date (or creation date) - can't be overdue before it started
                var choreStartDate = chore.StartDate?.Date 
                    ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);
                
                // Calculate if overdue within grace period
                var overduePeriod = CalculateOverduePeriod(chore.Frequency, lastCompletedAt, today, choreStartDate, chorePauses.GetValueOrDefault(choreId));
                
                if (overduePeriod != null)
                {
                    overdueChores.Add(new OverdueChoreDto(
                        choreId,
                        chore.DisplayName,
                        overduePeriod,
                        lastCompletedAt));
                }
            }
            
            result.Add(new MemberOverdueDto(
                memberId,
                member.Nickname,
                overdueChores.Count,
                overdueChores.OrderBy(c => c.DisplayName).ToList()));
        }
        
        // Sort: members with overdue chores first, then by count descending
        return new GetOverdueResponse(
            result.OrderByDescending(m => m.OverdueCount > 0)
                  .ThenByDescending(m => m.OverdueCount)
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

    private static string? CalculateOverduePeriod(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt, List<PauseWindow>? pauses)
    {
        if (frequency == null) return null;

        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today, choreCreatedAt, pauses),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today, choreCreatedAt, pauses),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today, choreCreatedAt, pauses),
            "once" => null, // One-time chores can't be overdue
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

