using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.MyChores;

public record GetMyChoresQuery(Guid HouseholdId, Guid MemberId);

public record MyChoreDto(
    Guid ChoreId,
    string DisplayName,
    string? FrequencyType,
    string? DueDescription);

public record MyOverdueChoreDto(
    Guid ChoreId,
    string DisplayName,
    string? FrequencyType,
    string OverduePeriod);

public record MyCompletedChoreDto(
    Guid ChoreId,
    string DisplayName,
    DateTime CompletedAt);

public record GetMyChoresResponse(
    List<MyChoreDto> Pending,
    List<MyOverdueChoreDto> Overdue,
    List<MyCompletedChoreDto> Completed);

internal class Handler(IEventStore store)
{
    public async Task<GetMyChoresResponse?> HandleAsync(GetMyChoresQuery request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        
        // Verify member exists
        var memberExists = householdEvents.OfType<MemberJoinedHousehold>()
            .Any(e => e.MemberId == request.MemberId);
        if (!memberExists) return null;
        
        var today = DateTime.UtcNow.Date;
        var currentWeekStart = GetMondayOfWeek(today);
        
        // Get all chores (excluding deleted)
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        
        // Get latest assignments
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        
        // Get this member's completions
        var myCompletions = choreEvents.OfType<ChoreCompleted>()
            .Where(c => c.CompletedByMemberId == request.MemberId)
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var pending = new List<MyChoreDto>();
        var overdue = new List<MyOverdueChoreDto>();
        var completed = new List<MyCompletedChoreDto>();
        
        foreach (var (choreId, chore) in chores)
        {
            // Check if assigned to me
            var assignment = assignments.GetValueOrDefault(choreId);
            var isAssignedToMe = assignment?.AssignToAll == true ||
                (assignment?.AssignedToMemberIds?.Contains(request.MemberId) ?? false);
            
            if (!isAssignedToMe) continue;
            
            var choreCompletions = myCompletions.GetValueOrDefault(choreId) ?? [];
            var choreStartDate = chore.StartDate?.Date 
                ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);
            
            // Categorize the chore
            var (status, periodCompletion, overduePeriod) = CategorizeChore(
                chore.Frequency, 
                choreCompletions, 
                today, 
                currentWeekStart,
                choreStartDate,
                chore.IsOptional);
            
            switch (status)
            {
                case ChoreStatus.Pending:
                    pending.Add(new MyChoreDto(
                        choreId,
                        chore.DisplayName,
                        chore.Frequency?.Type,
                        GetDueDescription(chore.Frequency, today, currentWeekStart)));
                    break;
                    
                case ChoreStatus.Overdue:
                    overdue.Add(new MyOverdueChoreDto(
                        choreId,
                        chore.DisplayName,
                        chore.Frequency?.Type,
                        overduePeriod!));
                    break;
                    
                case ChoreStatus.Completed:
                    completed.Add(new MyCompletedChoreDto(
                        choreId,
                        chore.DisplayName,
                        periodCompletion!.Value));
                    break;
            }
        }
        
        return new GetMyChoresResponse(
            pending.OrderBy(c => c.DisplayName).ToList(),
            overdue.OrderBy(c => c.DisplayName).ToList(),
            completed.OrderByDescending(c => c.CompletedAt).ToList());
    }
    
    private enum ChoreStatus { Pending, Overdue, Completed }
    
    private static (ChoreStatus Status, DateTime? PeriodCompletion, string? OverduePeriod) CategorizeChore(
        ChoreFrequency? frequency,
        List<ChoreCompleted> completions,
        DateTime today,
        DateTime currentWeekStart,
        DateTime choreStartDate,
        bool isOptional)
    {
        if (frequency == null)
        {
            // No frequency = one-time chore
            var anyCompletion = completions.MaxBy(c => c.CompletedAt);
            return anyCompletion != null 
                ? (ChoreStatus.Completed, anyCompletion.CompletedAt, null)
                : (ChoreStatus.Pending, null, null);
        }
        
        return frequency.Type.ToLower() switch
        {
            "daily" => CategorizeDailyChore(completions, today, choreStartDate, isOptional),
            "weekly" => CategorizeWeeklyChore(frequency.Days, completions, today, currentWeekStart, choreStartDate, isOptional),
            "interval" => CategorizeIntervalChore(frequency.IntervalDays ?? 1, completions, today, choreStartDate, isOptional),
            "once" => CategorizeOnceChore(completions),
            _ => (ChoreStatus.Pending, null, null)
        };
    }
    
    private static (ChoreStatus, DateTime?, string?) CategorizeDailyChore(
        List<ChoreCompleted> completions,
        DateTime today,
        DateTime choreStartDate,
        bool isOptional)
    {
        var yesterday = today.AddDays(-1);
        
        // Check if completed today
        var todayCompletion = completions.FirstOrDefault(c => c.CompletedAt.Date == today);
        if (todayCompletion != null)
            return (ChoreStatus.Completed, todayCompletion.CompletedAt, null);
        
        // Check if overdue (missed yesterday) - only for non-optional chores
        if (!isOptional && choreStartDate <= yesterday)
        {
            var yesterdayCompletion = completions.FirstOrDefault(c => c.CompletedAt.Date == yesterday);
            if (yesterdayCompletion == null)
                return (ChoreStatus.Overdue, null, "yesterday");
        }
        
        // Pending for today
        return (ChoreStatus.Pending, null, null);
    }
    
    private static (ChoreStatus, DateTime?, string?) CategorizeWeeklyChore(
        string[]? days,
        List<ChoreCompleted> completions,
        DateTime today,
        DateTime currentWeekStart,
        DateTime choreStartDate,
        bool isOptional)
    {
        var previousWeekStart = currentWeekStart.AddDays(-7);
        
        // Weekly-anyday: no specific days
        if (days == null || days.Length == 0)
        {
            // Check if completed this week
            var thisWeekCompletion = completions
                .Where(c => c.CompletedAt.Date >= currentWeekStart)
                .MaxBy(c => c.CompletedAt);
            if (thisWeekCompletion != null)
                return (ChoreStatus.Completed, thisWeekCompletion.CompletedAt, null);
            
            // Check if overdue (missed last week) - only for non-optional and if existed last week
            if (!isOptional && choreStartDate < currentWeekStart)
            {
                var lastWeekCompletion = completions
                    .FirstOrDefault(c => c.CompletedAt.Date >= previousWeekStart && c.CompletedAt.Date < currentWeekStart);
                if (lastWeekCompletion == null)
                    return (ChoreStatus.Overdue, null, "last week");
            }
            
            return (ChoreStatus.Pending, null, null);
        }
        
        // Weekly with specific days
        var requiredDays = days.Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true)).ToHashSet();
        
        // Check if completed this week on/after a required day
        var thisWeekCompletion2 = completions
            .Where(c => c.CompletedAt.Date >= currentWeekStart)
            .MaxBy(c => c.CompletedAt);
        if (thisWeekCompletion2 != null)
            return (ChoreStatus.Completed, thisWeekCompletion2.CompletedAt, null);
        
        // Check for overdue (missed last week's required day) - only for non-optional
        if (!isOptional)
        {
            for (int i = 0; i < 7; i++)
            {
                var checkDate = previousWeekStart.AddDays(i);
                if (!requiredDays.Contains(checkDate.DayOfWeek)) continue;
                if (choreStartDate > checkDate) continue;
                
                var completedOnOrAfter = completions.Any(c => c.CompletedAt.Date >= checkDate);
                if (!completedOnOrAfter)
                    return (ChoreStatus.Overdue, null, $"last {checkDate.DayOfWeek}");
            }
        }
        
        return (ChoreStatus.Pending, null, null);
    }
    
    private static (ChoreStatus, DateTime?, string?) CategorizeIntervalChore(
        int intervalDays,
        List<ChoreCompleted> completions,
        DateTime today,
        DateTime choreStartDate,
        bool isOptional)
    {
        var lastCompletion = completions.MaxBy(c => c.CompletedAt);
        
        if (lastCompletion == null)
        {
            // Never completed
            var firstDueDate = choreStartDate.AddDays(intervalDays);
            if (today < firstDueDate)
                return (ChoreStatus.Pending, null, null);
            
            var daysOverdue = (int)(today - firstDueDate).TotalDays;
            if (!isOptional && daysOverdue > 0 && daysOverdue <= intervalDays)
                return (ChoreStatus.Overdue, null, daysOverdue == 1 ? "yesterday" : $"{daysOverdue} days ago");
            
            return (ChoreStatus.Pending, null, null);
        }
        
        var nextDueDate = lastCompletion.CompletedAt.Date.AddDays(intervalDays);
        
        // If completed recently enough, it's completed
        if (today <= nextDueDate)
            return (ChoreStatus.Completed, lastCompletion.CompletedAt, null);
        
        // Overdue - but only within grace period (1 interval)
        var daysOverdue2 = (int)(today - nextDueDate).TotalDays;
        if (!isOptional && daysOverdue2 <= intervalDays)
            return (ChoreStatus.Overdue, null, daysOverdue2 == 1 ? "yesterday" : $"{daysOverdue2} days ago");
        
        // Past grace period = treat as pending (forgiven)
        return (ChoreStatus.Pending, null, null);
    }
    
    private static (ChoreStatus, DateTime?, string?) CategorizeOnceChore(List<ChoreCompleted> completions)
    {
        var completion = completions.MaxBy(c => c.CompletedAt);
        return completion != null
            ? (ChoreStatus.Completed, completion.CompletedAt, null)
            : (ChoreStatus.Pending, null, null);
    }
    
    private static string? GetDueDescription(ChoreFrequency? frequency, DateTime today, DateTime currentWeekStart)
    {
        if (frequency == null) return "one-time";
        
        return frequency.Type.ToLower() switch
        {
            "daily" => "today",
            "weekly" when frequency.Days is { Length: > 0 } => 
                $"on {string.Join(", ", frequency.Days)}",
            "weekly" => "this week",
            "interval" => $"every {frequency.IntervalDays} days",
            "once" => "one-time",
            _ => null
        };
    }
    
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}

internal static class MyChoresEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/my-chores", async (
            Guid householdId,
            Guid memberId,
            Handler handler) =>
        {
            var query = new GetMyChoresQuery(householdId, memberId);
            var result = await handler.HandleAsync(query);
            
            if (result == null)
                return Results.NotFound();
            
            return Results.Ok(result);
        });
    }
}
