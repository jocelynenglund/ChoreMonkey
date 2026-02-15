using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.TeamOverview;

public record TeamOverviewQuery(Guid HouseholdId, int PinCode);

public record ChoreStatusDto(
    Guid ChoreId,
    string DisplayName,
    string Status, // "completed", "pending", "overdue"
    string? OverduePeriod,
    DateTime? LastCompletedAt,
    bool IsOptional);

public record MemberOverviewDto(
    Guid MemberId,
    string Nickname,
    int TotalChores,
    int CompletedCount,
    int OverdueCount,
    List<ChoreStatusDto> Chores);

public record TeamOverviewResponse(List<MemberOverviewDto> Members);

internal class Handler(IEventStore store, ISender mediator)
{
    public async Task<TeamOverviewResponse?> HandleAsync(TeamOverviewQuery request)
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
            return null;
        }
        
        var today = DateTime.UtcNow.Date;
        
        // Get members via MemberLookup query
        var memberLookup = await mediator.Send(new MemberLookupQuery(request.HouseholdId));
        var members = memberLookup.Members;
        
        // Get deleted chore IDs
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
        // Get all chores (excluding deleted)
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
                    chore.IsOptional);
                
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
    
    private static (string Status, string? OverduePeriod) CalculateStatus(
        ChoreFrequency? frequency, 
        DateTime? lastCompleted, 
        DateTime today,
        DateTime choreStartDate,
        bool isOptional)
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
        var overduePeriod = CalculateOverduePeriod(frequency, lastCompleted, today, choreStartDate);
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
    
    private static string? CalculateOverduePeriod(ChoreFrequency? frequency, DateTime? lastCompleted, DateTime today, DateTime choreCreatedAt)
    {
        if (frequency == null) return null;
        
        return frequency.Type.ToLower() switch
        {
            "daily" => CalculateDailyOverdue(lastCompleted, today, choreCreatedAt),
            "weekly" => CalculateWeeklyOverdue(frequency.Days, lastCompleted, today, choreCreatedAt),
            "interval" => CalculateIntervalOverdue(frequency.IntervalDays ?? 1, lastCompleted, today, choreCreatedAt),
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

internal static class TeamOverviewEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/team", async (
            Guid householdId,
            HttpContext context,
            Handler handler) =>
        {
            var pinCodeHeader = context.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCodeHeader) || !int.TryParse(pinCodeHeader, out var pinCode))
            {
                return Results.Json(new { error = "PIN required" }, statusCode: 401);
            }
            
            var query = new TeamOverviewQuery(householdId, pinCode);
            var result = await handler.HandleAsync(query);
            
            if (result == null)
            {
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);
            }
            
            return Results.Ok(result);
        });
    }
}
