using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.MyChores;

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
    string OverduePeriod,
    string PeriodKey);  // For acknowledge-missed: "2024-02-13" or "2024-W06"

public record MyCompletedChoreDto(
    Guid ChoreId,
    string DisplayName,
    DateTime CompletedAt,
    string? CompletedByName = null);  // null = completed by me; set when someone else did a shared chore

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
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var yesterday = today.AddDays(-1);
        
        // Get all chores (excluding deleted)
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
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
        
        // Get latest assignments
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        
        // Get this member's completions
        var myCompletions = choreEvents.OfType<ChoreCompleted>()
            .Where(c => c.CompletedByMemberId == request.MemberId)
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get ALL household completions (for shared/assignedToAll chores)
        var allCompletions = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Member nicknames for "done by X" display — use latest nickname
        var memberNicknames = householdEvents.OfType<MemberJoinedHousehold>()
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.Last().Nickname);
        foreach (var rename in householdEvents.OfType<MemberNicknameChanged>())
            memberNicknames[rename.MemberId] = rename.NewNickname;
        
        // Get this member's acknowledged misses
        var myAcknowledgments = choreEvents.OfType<ChoreMissedAcknowledged>()
            .Where(a => a.MemberId == request.MemberId)
            .GroupBy(a => a.ChoreId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Period).ToHashSet());
        
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
            var acknowledgedPeriods = myAcknowledgments.GetValueOrDefault(choreId) ?? [];
            var choreStartDate = chore.StartDate?.Date 
                ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);

            // For assignedToAll chores: check if ANYONE has done it this period
            var isShared = assignment?.AssignToAll == true;
            var completionsToCheck = isShared
                ? allCompletions.GetValueOrDefault(choreId) ?? []
                : choreCompletions;
            
            // Categorize for CURRENT period (pending vs completed)
            var currentPeriodResult = GetCurrentPeriodStatus(
                chore.Frequency, 
                completionsToCheck, 
                today, 
                currentWeekStart);
            
            if (currentPeriodResult.IsCompleted)
            {
                // Was it done by someone else?
                string? completedByName = null;
                if (isShared && currentPeriodResult.CompletedByMemberId.HasValue 
                    && currentPeriodResult.CompletedByMemberId.Value != request.MemberId)
                {
                    memberNicknames.TryGetValue(currentPeriodResult.CompletedByMemberId.Value, out completedByName);
                }

                completed.Add(new MyCompletedChoreDto(
                    choreId,
                    chore.DisplayName,
                    currentPeriodResult.CompletedAt!.Value,
                    completedByName));
            }
            else
            {
                pending.Add(new MyChoreDto(
                    choreId,
                    chore.DisplayName,
                    chore.Frequency?.Type,
                    GetDueDescription(chore.Frequency, today, currentWeekStart)));
            }
            
            // Check for PREVIOUS period overdue (separate from current period)
            if (!chore.IsOptional)
            {
                var overdueResult = GetPreviousPeriodOverdue(
                    chore.Frequency,
                    choreCompletions,
                    acknowledgedPeriods,
                    today,
                    yesterday,
                    currentWeekStart,
                    previousWeekStart,
                    choreStartDate);
                
                if (overdueResult.IsOverdue)
                {
                    overdue.Add(new MyOverdueChoreDto(
                        choreId,
                        chore.DisplayName,
                        chore.Frequency?.Type,
                        overdueResult.OverduePeriod!,
                        overdueResult.PeriodKey!));
                }
            }
        }
        
        return new GetMyChoresResponse(
            pending.OrderBy(c => c.DisplayName).ToList(),
            overdue.OrderBy(c => c.DisplayName).ToList(),
            completed.OrderByDescending(c => c.CompletedAt).ToList());
    }
    
    private record CurrentPeriodResult(bool IsCompleted, DateTime? CompletedAt, Guid? CompletedByMemberId = null);
    private record OverdueResult(bool IsOverdue, string? OverduePeriod, string? PeriodKey);
    
    private static CurrentPeriodResult GetCurrentPeriodStatus(
        ChoreFrequency? frequency,
        List<ChoreCompleted> completions,
        DateTime today,
        DateTime currentWeekStart)
    {
        if (frequency == null || frequency.Type.ToLower() == "once")
        {
            var completion = completions.MaxBy(c => c.CompletedAt);
            return new CurrentPeriodResult(completion != null, completion?.CompletedAt, completion?.CompletedByMemberId);
        }
        
        return frequency.Type.ToLower() switch
        {
            "daily" => GetDailyCurrentStatus(completions, today),
            "weekly" => GetWeeklyCurrentStatus(completions, currentWeekStart),
            "interval" => GetIntervalCurrentStatus(frequency.IntervalDays ?? 1, completions, today),
            _ => new CurrentPeriodResult(false, null)
        };
    }
    
    private static CurrentPeriodResult GetDailyCurrentStatus(List<ChoreCompleted> completions, DateTime today)
    {
        var todayCompletion = completions.FirstOrDefault(c => c.CompletedAt.Date == today);
        return new CurrentPeriodResult(todayCompletion != null, todayCompletion?.CompletedAt, todayCompletion?.CompletedByMemberId);
    }
    
    private static CurrentPeriodResult GetWeeklyCurrentStatus(List<ChoreCompleted> completions, DateTime currentWeekStart)
    {
        var thisWeekCompletion = completions
            .Where(c => c.CompletedAt.Date >= currentWeekStart)
            .MaxBy(c => c.CompletedAt);
        return new CurrentPeriodResult(thisWeekCompletion != null, thisWeekCompletion?.CompletedAt, thisWeekCompletion?.CompletedByMemberId);
    }
    
    private static CurrentPeriodResult GetIntervalCurrentStatus(int intervalDays, List<ChoreCompleted> completions, DateTime today)
    {
        var lastCompletion = completions.MaxBy(c => c.CompletedAt);
        if (lastCompletion == null) return new CurrentPeriodResult(false, null);
        
        var nextDue = lastCompletion.CompletedAt.Date.AddDays(intervalDays);
        return new CurrentPeriodResult(today < nextDue, lastCompletion.CompletedAt, lastCompletion.CompletedByMemberId);
    }
    
    private static OverdueResult GetPreviousPeriodOverdue(
        ChoreFrequency? frequency,
        List<ChoreCompleted> completions,
        HashSet<string> acknowledgedPeriods,
        DateTime today,
        DateTime yesterday,
        DateTime currentWeekStart,
        DateTime previousWeekStart,
        DateTime choreStartDate)
    {
        if (frequency == null || frequency.Type.ToLower() == "once")
        {
            return new OverdueResult(false, null, null);
        }
        
        return frequency.Type.ToLower() switch
        {
            "daily" => GetDailyOverdue(completions, acknowledgedPeriods, yesterday, choreStartDate),
            "weekly" => GetWeeklyOverdue(frequency.Days, completions, acknowledgedPeriods, currentWeekStart, previousWeekStart, choreStartDate),
            "interval" => GetIntervalOverdue(frequency.IntervalDays ?? 1, completions, acknowledgedPeriods, today, choreStartDate),
            _ => new OverdueResult(false, null, null)
        };
    }
    
    private static OverdueResult GetDailyOverdue(
        List<ChoreCompleted> completions,
        HashSet<string> acknowledgedPeriods,
        DateTime yesterday,
        DateTime choreStartDate)
    {
        // Can't be overdue if chore didn't exist yesterday
        if (choreStartDate > yesterday)
            return new OverdueResult(false, null, null);
        
        var periodKey = yesterday.ToString("yyyy-MM-dd");
        
        // Check if acknowledged
        if (acknowledgedPeriods.Contains(periodKey))
            return new OverdueResult(false, null, null);
        
        // Check if completed yesterday
        var yesterdayCompletion = completions.FirstOrDefault(c => c.CompletedAt.Date == yesterday);
        if (yesterdayCompletion != null)
            return new OverdueResult(false, null, null);
        
        return new OverdueResult(true, "yesterday", periodKey);
    }
    
    private static OverdueResult GetWeeklyOverdue(
        string[]? days,
        List<ChoreCompleted> completions,
        HashSet<string> acknowledgedPeriods,
        DateTime currentWeekStart,
        DateTime previousWeekStart,
        DateTime choreStartDate)
    {
        // Can't be overdue if chore didn't exist last week
        if (choreStartDate >= currentWeekStart)
            return new OverdueResult(false, null, null);
        
        var periodKey = GetWeekPeriod(previousWeekStart);
        
        // Check if acknowledged
        if (acknowledgedPeriods.Contains(periodKey))
            return new OverdueResult(false, null, null);
        
        // Check if completed last week
        var lastWeekCompletion = completions.FirstOrDefault(c => 
            c.CompletedAt.Date >= previousWeekStart && c.CompletedAt.Date < currentWeekStart);
        if (lastWeekCompletion != null)
            return new OverdueResult(false, null, null);
        
        return new OverdueResult(true, "last week", periodKey);
    }
    
    private static OverdueResult GetIntervalOverdue(
        int intervalDays,
        List<ChoreCompleted> completions,
        HashSet<string> acknowledgedPeriods,
        DateTime today,
        DateTime choreStartDate)
    {
        var lastCompletion = completions.MaxBy(c => c.CompletedAt);
        
        DateTime dueDate;
        if (lastCompletion == null)
        {
            dueDate = choreStartDate.AddDays(intervalDays);
        }
        else
        {
            dueDate = lastCompletion.CompletedAt.Date.AddDays(intervalDays);
        }
        
        if (today <= dueDate)
            return new OverdueResult(false, null, null);
        
        var daysOverdue = (int)(today - dueDate).TotalDays;
        
        // Only show if within grace period (1 interval)
        if (daysOverdue > intervalDays)
            return new OverdueResult(false, null, null);
        
        var periodKey = dueDate.ToString("yyyy-MM-dd");
        
        // Check if acknowledged
        if (acknowledgedPeriods.Contains(periodKey))
            return new OverdueResult(false, null, null);
        
        var overduePeriod = daysOverdue == 1 ? "yesterday" : $"{daysOverdue} days ago";
        return new OverdueResult(true, overduePeriod, periodKey);
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
    
    private static string GetWeekPeriod(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
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
