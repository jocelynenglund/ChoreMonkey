using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetCurrentPeriod;

public record GetCurrentPeriodQuery(Guid HouseholdId, Guid? MemberId = null);

public record MissedChoreDto(Guid ChoreId, string ChoreName, string Period, decimal Deduction);
public record BonusChoreDto(Guid ChoreId, string ChoreName, DateTime CompletedAt, decimal Bonus);

public record MemberPeriodSummary(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal Projected,
    List<MissedChoreDto> MissedChores,
    List<BonusChoreDto> BonusChores);

public record CurrentPeriodResponse(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<MemberPeriodSummary> Members);

internal class Handler(IEventStore store)
{
    private const int GracePeriodDays = 2;
    
    public async Task<CurrentPeriodResponse> HandleAsync(GetCurrentPeriodQuery request)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndExclusive = periodStart.AddMonths(1); // End-exclusive for accurate boundary

        // Fetch all relevant streams
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var salaryEvents = await store.FetchEventsAsync(salaryStreamId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Get member info
        var memberNames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(e => e.MemberId, e => e.Nickname);

        // Get salary configs
        var salaryConfigs = salaryEvents
            .OfType<MemberSalarySet>()
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get chore rates
        var choreRates = salaryEvents
            .OfType<ChoreRatesSet>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get chores
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        var chores = choreEvents
            .OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);

        // Get assignments
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get completions in period grouped by (chore, member)
        var completionsInPeriod = choreEvents
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt < periodEndExclusive)
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all completions for last-completed tracking
        var allCompletions = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CompletedAt).First());

        // Calculate per-member summaries
        var members = new List<MemberPeriodSummary>();
        var allMemberIds = memberNames.Keys.ToList();

        if (request.MemberId.HasValue)
        {
            allMemberIds = allMemberIds.Where(id => id == request.MemberId.Value).ToList();
        }

        foreach (var memberId in allMemberIds)
        {
            var config = salaryConfigs.GetValueOrDefault(memberId);
            var baseSalary = config?.BaseSalary ?? 0m;
            var deductionMultiplier = config?.DeductionMultiplier ?? 1.0m;
            var bonusMultiplier = config?.BonusMultiplier ?? 1.0m;

            var missedChores = new List<MissedChoreDto>();
            var bonusChores = new List<BonusChoreDto>();
            decimal totalDeductions = 0m;
            decimal totalBonuses = 0m;

            foreach (var (choreId, chore) in chores)
            {
                // Check if assigned to this member
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
                if (!isAssigned) continue;

                var rates = choreRates.GetValueOrDefault(choreId);
                var lastCompletion = allCompletions.GetValueOrDefault((choreId, memberId));

                if (chore.IsOptional)
                {
                    // Bonus chores: count completions in period
                    var completions = completionsInPeriod.GetValueOrDefault((choreId, memberId));
                    if (completions != null)
                    {
                        var bonusRate = rates?.BonusRate ?? 0m;
                        foreach (var completion in completions)
                        {
                            var bonus = bonusRate * bonusMultiplier;
                            bonusChores.Add(new BonusChoreDto(choreId, chore.DisplayName, completion.CompletedAt, bonus));
                            totalBonuses += bonus;
                        }
                    }
                }
                else
                {
                    // Required chores: check for missed instances (>2 days overdue)
                    var missed = CalculateMissedInstances(
                        chore, 
                        lastCompletion?.CompletedAt, 
                        periodStart, 
                        today,
                        completionsInPeriod.GetValueOrDefault((choreId, memberId)));
                    
                    var deductionRate = rates?.DeductionRate ?? chore.MissedDeduction;
                    foreach (var period in missed)
                    {
                        var deduction = deductionRate * deductionMultiplier;
                        missedChores.Add(new MissedChoreDto(choreId, chore.DisplayName, period, deduction));
                        totalDeductions += deduction;
                    }
                }
            }

            var projected = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);

            members.Add(new MemberPeriodSummary(
                memberId,
                memberNames.GetValueOrDefault(memberId, "Unknown"),
                baseSalary,
                totalDeductions,
                totalBonuses,
                projected,
                missedChores,
                bonusChores));
        }

        return new CurrentPeriodResponse(periodStart, periodEndExclusive.AddDays(-1), members);
    }

    private List<string> CalculateMissedInstances(
        ChoreCreated chore, 
        DateTime? lastCompleted, 
        DateTime periodStart,
        DateTime today,
        List<ChoreCompleted>? completionsInPeriod)
    {
        var missed = new List<string>();
        var frequency = chore.Frequency;
        if (frequency == null) return missed;

        var choreStart = chore.StartDate?.Date ?? periodStart;
        var effectiveStart = choreStart > periodStart ? choreStart : periodStart;
        
        // Only count as missed if grace period has passed
        var cutoffDate = today.AddDays(-GracePeriodDays);

        switch (frequency.Type.ToLower())
        {
            case "daily":
                missed = CalculateDailyMissed(effectiveStart, cutoffDate, completionsInPeriod);
                break;
            case "weekly":
                missed = CalculateWeeklyMissed(frequency.Days, effectiveStart, cutoffDate, completionsInPeriod);
                break;
            case "interval":
                missed = CalculateIntervalMissed(frequency.IntervalDays ?? 1, effectiveStart, cutoffDate, lastCompleted, completionsInPeriod);
                break;
        }

        return missed;
    }

    private static List<string> CalculateDailyMissed(DateTime start, DateTime cutoff, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();
        
        for (var date = start; date <= cutoff; date = date.AddDays(1))
        {
            if (!completedDates.Contains(date))
            {
                missed.Add(date.ToString("yyyy-MM-dd"));
            }
        }
        return missed;
    }

    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static List<string> CalculateWeeklyMissed(string[]? days, DateTime start, DateTime cutoff, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        if (days == null || days.Length == 0)
        {
            // Weekly anytime - one completion per week required
            var currentWeek = GetMondayOfWeek(start);
            var cutoffWeek = GetMondayOfWeek(cutoff);
            
            while (currentWeek <= cutoffWeek)
            {
                var weekEnd = currentWeek.AddDays(6);
                var completedThisWeek = completedDates.Any(d => d >= currentWeek && d <= weekEnd);
                
                if (!completedThisWeek && weekEnd <= cutoff)
                {
                    var weekNum = System.Globalization.CultureInfo.InvariantCulture.Calendar
                        .GetWeekOfYear(currentWeek, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    missed.Add($"{currentWeek.Year}-W{weekNum:D2}");
                }
                currentWeek = currentWeek.AddDays(7);
            }
        }
        else
        {
            // Specific days required
            var requiredDays = days.Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true)).ToHashSet();
            
            for (var date = start; date <= cutoff; date = date.AddDays(1))
            {
                if (requiredDays.Contains(date.DayOfWeek) && !completedDates.Contains(date))
                {
                    missed.Add(date.ToString("yyyy-MM-dd"));
                }
            }
        }
        
        return missed;
    }

    private static List<string> CalculateIntervalMissed(int intervalDays, DateTime start, DateTime cutoff, DateTime? lastCompleted, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).OrderBy(d => d).ToList() ?? new List<DateTime>();
        
        var lastDue = lastCompleted?.Date ?? start.AddDays(-1);
        var idx = 0;
        
        while (true)
        {
            var nextDue = lastDue.AddDays(intervalDays);
            if (nextDue > cutoff) break;
            
            // Check if completed on or before next due date
            while (idx < completedDates.Count && completedDates[idx] <= nextDue)
            {
                lastDue = completedDates[idx];
                idx++;
                nextDue = lastDue.AddDays(intervalDays);
                if (nextDue > cutoff) break;
            }
            
            if (nextDue > cutoff) break;
            
            // Not completed - it's missed
            missed.Add(nextDue.ToString("yyyy-MM-dd"));
            lastDue = nextDue;
        }
        
        return missed;
    }
}

internal static class GetCurrentPeriodEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/current", async (
            Guid householdId,
            Guid? memberId,
            Handler handler) =>
        {
            var query = new GetCurrentPeriodQuery(householdId, memberId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
