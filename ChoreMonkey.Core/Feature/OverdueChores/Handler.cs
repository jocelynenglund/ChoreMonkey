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
                
                // Calculate if overdue
                var overdueDays = CalculateOverdueDays(chore.Frequency, lastCompletedAt, today);
                
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
    
    private static int CalculateOverdueDays(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today)
    {
        if (frequency == null) return 0;
        
        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today),
            "once" => 0, // One-time chores can't be overdue
            _ => 0
        };
    }
    
    private static int CalculateDailyOverdue(DateTime? lastCompleted, DateTime today)
    {
        if (lastCompleted == null)
        {
            // If never completed, consider it 1 day overdue (should have been done yesterday)
            return 1;
        }
        
        var lastCompletedDate = lastCompleted.Value.Date;
        
        // If completed today, not overdue
        if (lastCompletedDate >= today) return 0;
        
        // If completed yesterday, not overdue (they have today to do it)
        if (lastCompletedDate >= today.AddDays(-1)) return 0;
        
        // Overdue by number of days since it should have been done
        return (int)(today - lastCompletedDate).TotalDays - 1;
    }
    
    private static int CalculateWeeklyOverdue(string[]? days, DateTime? lastCompleted, DateTime today)
    {
        if (days == null || days.Length == 0) return 0;
        
        // Find the most recent required day that has passed
        var requiredDays = days
            .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
            .ToHashSet();
        
        // Look back up to 7 days to find the last required day
        for (int i = 1; i <= 7; i++)
        {
            var checkDate = today.AddDays(-i);
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
    
    private static int CalculateIntervalOverdue(int intervalDays, DateTime? lastCompleted, DateTime today)
    {
        if (lastCompleted == null)
        {
            // If never completed, consider it overdue by the interval
            return intervalDays;
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
