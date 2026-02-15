using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.OverdueChores;

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

internal class Handler(IEventStore store, ISender mediator)
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
        
        // Get members via MemberLookup query
        var memberLookup = await mediator.Send(new MemberLookupQuery(request.HouseholdId));
        var members = memberLookup.Members;
        
        // Get deleted chore IDs
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
        // Get all chores with frequencies (excluding deleted)
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(e => !deletedChoreIds.Contains(e.ChoreId))
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
    
    private static string? CalculateDailyOverdue(DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        var yesterday = today.AddDays(-1);
        
        if (choreCreatedAt > yesterday) return null;
        if (lastCompleted?.Date >= today) return null;
        if (lastCompleted?.Date >= yesterday) return null;
        
        return "yesterday";
    }
    
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string? CalculateWeeklyOverdue(string[]? days, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        var currentWeekStart = GetMondayOfWeek(today);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        
        if (days == null || days.Length == 0)
        {
            if (choreCreatedAt >= previousWeekStart) return null;
            if (lastCompleted?.Date >= currentWeekStart) return null;
            if (lastCompleted?.Date >= previousWeekStart) return null;
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
            
            return $"last {checkDate.DayOfWeek}";
        }
        
        return null;
    }
    
    private static string? CalculateIntervalOverdue(int intervalDays, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
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
        
        var daysOverdue = (int)(today - dueDate).TotalDays;
        
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
