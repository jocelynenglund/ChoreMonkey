using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.OverdueChores;

public record GetOverdueQuery(Guid HouseholdId);

public record OverdueChoreDto(
    Guid ChoreId,
    string DisplayName,
    int OverdueDays,
    DateTime? LastCompleted);

public record MemberOverdueDto(
    Guid MemberId,
    string Nickname,
    int OverdueCount,
    List<OverdueChoreDto> Chores);

public record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);

internal class Handler(IEventStore store)
{
    public async Task<GetOverdueResponse> HandleAsync(GetOverdueQuery request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        
        var today = DateTime.UtcNow.Date;
        
        // Get all members
        var members = householdEvents.OfType<MemberJoinedHousehold>()
            .ToDictionary(e => e.MemberId, e => e.Nickname);
        
        // Get all chores with frequencies
        var chores = choreEvents.OfType<ChoreCreated>()
            .ToDictionary(e => e.ChoreId);
        
        // Get latest assignments
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        
        // Get completions grouped by chore and member
        var completions = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(
                g => g.Key, 
                g => g.OrderByDescending(c => c.CompletedAt).First());
        
        var result = new List<MemberOverdueDto>();
        
        foreach (var (memberId, nickname) in members)
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
                
                // Get chore creation date - can't be overdue before it existed
                var choreCreatedAt = DateTime.TryParse(chore.TimestampUtc, out var parsed) 
                    ? parsed.Date 
                    : today;
                
                // Calculate if overdue (considering creation date)
                var overdueDays = CalculateOverdueDays(chore.Frequency, lastCompletedAt, today, choreCreatedAt);
                
                if (overdueDays > 0)
                {
                    overdueChores.Add(new OverdueChoreDto(
                        choreId,
                        chore.DisplayName,
                        overdueDays,
                        lastCompletedAt));
                }
            }
            
            result.Add(new MemberOverdueDto(
                memberId,
                nickname,
                overdueChores.Count,
                overdueChores.OrderByDescending(c => c.OverdueDays).ToList()));
        }
        
        // Sort: members with overdue chores first, then by count descending
        return new GetOverdueResponse(
            result.OrderByDescending(m => m.OverdueCount > 0)
                  .ThenByDescending(m => m.OverdueCount)
                  .ToList());
    }
    
    private static int CalculateOverdueDays(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        if (frequency == null) return 0;
        
        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today, choreCreatedAt),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today, choreCreatedAt),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today, choreCreatedAt),
            "once" => 0, // One-time chores can't be overdue
            _ => 0
        };
    }
    
    private static int CalculateDailyOverdue(DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        // Can't be overdue if created today or yesterday (give them today to do it)
        if (choreCreatedAt >= today.AddDays(-1)) return 0;
        
        if (lastCompleted == null)
        {
            // Never completed - overdue since the day after creation
            var firstDueDate = choreCreatedAt.AddDays(1);
            if (firstDueDate >= today) return 0;
            return (int)(today - firstDueDate).TotalDays;
        }
        
        var lastCompletedDate = lastCompleted.Value.Date;
        
        // If completed today, not overdue
        if (lastCompletedDate >= today) return 0;
        
        // If completed yesterday, not overdue (they have today to do it)
        if (lastCompletedDate >= today.AddDays(-1)) return 0;
        
        // Overdue by number of days since it should have been done
        return (int)(today - lastCompletedDate).TotalDays - 1;
    }
    
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static int CalculateWeeklyOverdue(string[]? days, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        // Weekly-anyday: no specific days = must complete once per week (Mon-Sun)
        if (days == null || days.Length == 0)
        {
            return CalculateWeeklyAnydayOverdue(lastCompleted, today, choreCreatedAt);
        }
        
        // Weekly with specific days
        var requiredDays = days
            .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
            .ToHashSet();
        
        // Look back up to 7 days to find the last required day
        for (int i = 1; i <= 7; i++)
        {
            var checkDate = today.AddDays(-i);
            
            // Don't count days before the chore was created
            if (checkDate < choreCreatedAt) continue;
            
            if (requiredDays.Contains(checkDate.DayOfWeek))
            {
                // This was a required day - was it completed?
                if (lastCompleted == null || lastCompleted.Value.Date < checkDate)
                {
                    return i; // Overdue by this many days
                }
                return 0; // Was completed on or after the required day
            }
        }
        
        return 0;
    }

    private static int CalculateWeeklyAnydayOverdue(DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        var currentWeekStart = GetMondayOfWeek(today);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        
        // If chore was created this week, can't be overdue yet
        if (choreCreatedAt >= currentWeekStart) return 0;
        
        // Check if completed this week
        if (lastCompleted != null && lastCompleted.Value.Date >= currentWeekStart)
        {
            return 0; // Completed this week, not overdue
        }
        
        // Check if completed last week
        if (lastCompleted != null && lastCompleted.Value.Date >= previousWeekStart)
        {
            return 0; // Completed last week, give them this week
        }
        
        // Overdue - calculate how many days since last week ended (or since creation)
        if (lastCompleted == null)
        {
            // Never completed - overdue since the week after creation ended
            var creationWeekStart = GetMondayOfWeek(choreCreatedAt);
            var firstDueWeekEnd = creationWeekStart.AddDays(7); // End of creation week
            if (today < firstDueWeekEnd) return 0; // Still in first week
            return (int)(today - firstDueWeekEnd).TotalDays;
        }
        
        // Last completed more than a week ago
        var lastCompletedWeekEnd = GetMondayOfWeek(lastCompleted.Value).AddDays(7);
        return (int)(today - lastCompletedWeekEnd).TotalDays;
    }
    
    private static int CalculateIntervalOverdue(int intervalDays, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        if (lastCompleted == null)
        {
            // If never completed, check if enough days have passed since creation
            var daysSinceCreation = (int)(today - choreCreatedAt).TotalDays;
            if (daysSinceCreation <= intervalDays) return 0; // Not enough time has passed
            return daysSinceCreation - intervalDays;
        }
        
        var daysSinceCompletion = (int)(today - lastCompleted.Value.Date).TotalDays;
        
        if (daysSinceCompletion <= intervalDays) return 0;
        
        return daysSinceCompletion - intervalDays;
    }
}

internal static class OverdueChoresEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/overdue", async (
            Guid householdId,
            Handler handler) =>
        {
            var query = new GetOverdueQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
