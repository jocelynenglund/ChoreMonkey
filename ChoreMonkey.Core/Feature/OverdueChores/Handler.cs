using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.OverdueChores;

public record GetOverdueQuery(Guid HouseholdId, int PinCode);

public record OverdueChoreDto(
    Guid ChoreId,
    string DisplayName,
    string OverduePeriod,
    DateTime? LastCompleted);

public record MemberOverdueDto(
    Guid MemberId,
    string Nickname,
    int OverdueCount,
    List<OverdueChoreDto> Chores);

public record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);

internal class Handler(IEventStore store)
{
    public async Task<GetOverdueResponse?> HandleAsync(GetOverdueQuery request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        
        // Verify admin access
        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null) return null;
        
        var adminPinHash = householdEvents.OfType<AdminPinChanged>()
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        
        if (!PinHasher.VerifyPin(request.PinCode, adminPinHash))
        {
            return null; // Not admin - return null (endpoint will return 403)
        }
        
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
                
                // Get chore start date (or creation date) - can't be overdue before it started
                var choreStartDate = chore.StartDate?.Date 
                    ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);
                
                // Calculate if overdue within grace period
                var overduePeriod = CalculateOverduePeriod(chore.Frequency, lastCompletedAt, today, choreStartDate);
                
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
                nickname,
                overdueChores.Count,
                overdueChores.OrderBy(c => c.DisplayName).ToList()));
        }
        
        // Sort: members with overdue chores first, then by count descending
        return new GetOverdueResponse(
            result.OrderByDescending(m => m.OverdueCount > 0)
                  .ThenByDescending(m => m.OverdueCount)
                  .ToList());
    }
    
    private static string? CalculateOverduePeriod(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        if (frequency == null) return null;
        
        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today, choreCreatedAt),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today, choreCreatedAt),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today, choreCreatedAt),
            "once" => null, // One-time chores can't be overdue
            _ => null
        };
    }
    
    /// <summary>
    /// Daily chores: only overdue if missed YESTERDAY (grace period = 1 day)
    /// Older misses are forgiven - we only show the most recent missed day
    /// </summary>
    private static string? CalculateDailyOverdue(DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        var yesterday = today.AddDays(-1);
        
        // Can't be overdue if chore didn't exist yesterday
        if (choreCreatedAt > yesterday) return null;
        
        // If completed today, not overdue
        if (lastCompleted?.Date >= today) return null;
        
        // If completed yesterday, not overdue
        if (lastCompleted?.Date >= yesterday) return null;
        
        // Missed yesterday - that's the only overdue we show (grace period)
        return "yesterday";
    }
    
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>
    /// Weekly chores: only overdue if missed LAST WEEK (grace period = 1 week)
    /// Older weeks are forgiven - we only show the most recent missed week
    /// </summary>
    private static string? CalculateWeeklyOverdue(string[]? days, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        var currentWeekStart = GetMondayOfWeek(today);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        
        // Weekly-anyday: no specific days = must complete once per week (Mon-Sun)
        if (days == null || days.Length == 0)
        {
            // Can't be overdue if created this week or last week
            if (choreCreatedAt >= previousWeekStart) return null;
            
            // If completed this week, not overdue
            if (lastCompleted?.Date >= currentWeekStart) return null;
            
            // If completed last week, not overdue (that's the grace period)
            if (lastCompleted?.Date >= previousWeekStart) return null;
            
            // Missed last week - that's the only overdue we show
            return "last week";
        }
        
        // Weekly with specific days - check if they missed last week's required day
        var requiredDays = days
            .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
            .ToHashSet();
        
        // Find last week's required day(s)
        for (int i = 0; i < 7; i++)
        {
            var checkDate = previousWeekStart.AddDays(i);
            
            if (!requiredDays.Contains(checkDate.DayOfWeek)) continue;
            
            // Found a required day last week - was it already created then?
            if (choreCreatedAt > checkDate) continue;
            
            // Check if completed on or after that day (up to now)
            if (lastCompleted?.Date >= checkDate) return null;
            
            // Missed last week's required day - overdue within grace period
            return $"last {checkDate.DayOfWeek}";
        }
        
        return null;
    }
    
    /// <summary>
    /// Interval chores: only overdue within 1 interval grace period
    /// If more than 2 intervals have passed, only show 1 interval overdue
    /// </summary>
    private static string? CalculateIntervalOverdue(int intervalDays, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        DateTime dueDate;
        
        if (lastCompleted == null)
        {
            // First due date is intervalDays after creation
            dueDate = choreCreatedAt.AddDays(intervalDays);
        }
        else
        {
            // Due date is intervalDays after last completion
            dueDate = lastCompleted.Value.Date.AddDays(intervalDays);
        }
        
        // Not due yet
        if (today <= dueDate) return null;
        
        var daysOverdue = (int)(today - dueDate).TotalDays;
        
        // Only show if within grace period (1 interval)
        if (daysOverdue > intervalDays) return null;
        
        if (daysOverdue == 1) return "yesterday";
        return $"{daysOverdue} days ago";
    }
}

internal static class OverdueChoresEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/overdue", async (
            Guid householdId,
            HttpContext context,
            Handler handler) =>
        {
            var pinCodeHeader = context.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCodeHeader) || !int.TryParse(pinCodeHeader, out var pinCode))
            {
                return Results.Json(new { error = "PIN required" }, statusCode: 401);
            }
            
            var query = new GetOverdueQuery(householdId, pinCode);
            var result = await handler.HandleAsync(query);
            
            if (result == null)
            {
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);
            }
            
            return Results.Ok(result);
        });
    }
}
