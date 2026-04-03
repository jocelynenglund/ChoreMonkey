using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.ClosePeriod;

public record ClosePeriodCommand(Guid HouseholdId);

public record ClosePeriodRequest();

public record MissedChoreDto(Guid ChoreId, string ChoreName, string Period, decimal Deduction);
public record BonusChoreDto(Guid ChoreId, string ChoreName, DateTime CompletedAt, decimal Bonus);

public record PayoutSummaryDto(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal NetPay,
    List<MissedChoreDto> MissedChores,
    List<BonusChoreDto> BonusChores);

public record ClosePeriodResponse(
    Guid PeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayoutSummaryDto> Payouts);

internal class Handler(IEventStore store, ISender mediator)
{
    public async Task<IResult> HandleAsync(ClosePeriodCommand request)
    {
        var today = DateTime.UtcNow.Date;

        // Fetch all relevant streams
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var salaryEvents = await store.FetchEventsAsync(salaryStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Determine payday from configuration, default to 25
        var paydayDay = salaryEvents
            .OfType<PaydayConfigured>()
            .LastOrDefault()?.PaydayDayOfMonth ?? 25;

        // Compute the most recently completed period (payday must have passed)
        var (periodStart, periodEnd) = GetLastCompletedPayPeriod(today, paydayDay);

        // Guard: refuse to close a period that hasn't ended yet
        if (today < periodEnd)
        {
            return Results.BadRequest(new { error = $"Period has not ended yet. It closes on {periodEnd:yyyy-MM-dd}." });
        }

        var periodId = Guid.NewGuid();

        // Get active members with current nicknames
        var memberLookup = await mediator.Send(new MemberLookupQuery(request.HouseholdId));
        var activeMembers = memberLookup.Members;

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

        // Get completions in period (end-exclusive)
        var completionsInPeriod = choreEvents
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt < periodEnd.AddDays(1))
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all completions for last-completed tracking
        var allCompletions = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CompletedAt).First());

        // Calculate payouts
        var payouts = new List<PeriodPayout>();
        var payoutDtos = new List<PayoutSummaryDto>();

        foreach (var memberId in activeMembers.Keys)
        {
            var config = salaryConfigs.GetValueOrDefault(memberId);
            var baseSalary = config?.BaseSalary ?? 0m;
            var deductionMultiplier = config?.DeductionMultiplier ?? 1.0m;
            var bonusMultiplier = config?.BonusMultiplier ?? 1.0m;

            var deductions = new List<PayoutDeduction>();
            var bonuses = new List<PayoutBonus>();
            var missedChoresDto = new List<MissedChoreDto>();
            var bonusChoresDto = new List<BonusChoreDto>();
            decimal totalDeductions = 0m;
            decimal totalBonuses = 0m;

            foreach (var (choreId, chore) in chores)
            {
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
                if (!isAssigned) continue;

                var rates = choreRates.GetValueOrDefault(choreId);
                var lastCompletion = allCompletions.GetValueOrDefault((choreId, memberId));

                if (chore.IsOptional)
                {
                    // Bonus chores
                    var completions = completionsInPeriod.GetValueOrDefault((choreId, memberId));
                    if (completions != null)
                    {
                        var bonusRate = rates?.BonusRate ?? 0m;
                        foreach (var completion in completions)
                        {
                            var amount = bonusRate * bonusMultiplier;
                            bonuses.Add(new PayoutBonus(choreId, chore.DisplayName, bonusRate, bonusMultiplier, amount));
                            bonusChoresDto.Add(new BonusChoreDto(choreId, chore.DisplayName, completion.CompletedAt, amount));
                            totalBonuses += amount;
                        }
                    }
                }
                else
                {
                    // Required chores - calculate missed for entire period (no grace period when closing)
                    var missed = CalculateMissedInstances(
                        chore, 
                        lastCompletion?.CompletedAt, 
                        periodStart, 
                        periodEnd,
                        completionsInPeriod.GetValueOrDefault((choreId, memberId)));
                    
                    var deductionRate = rates?.DeductionRate ?? chore.MissedDeduction;
                    foreach (var period in missed)
                    {
                        var amount = deductionRate * deductionMultiplier;
                        deductions.Add(new PayoutDeduction(choreId, chore.DisplayName, deductionRate, deductionMultiplier, amount));
                        missedChoresDto.Add(new MissedChoreDto(choreId, chore.DisplayName, period, amount));
                        totalDeductions += amount;
                    }
                }
            }

            var netPay = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);
            var memberName = activeMembers.GetValueOrDefault(memberId)?.Nickname ?? "Unknown";

            payouts.Add(new PeriodPayout(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay,
                deductions.AsReadOnly(),
                bonuses.AsReadOnly()));

            payoutDtos.Add(new PayoutSummaryDto(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay,
                missedChoresDto,
                bonusChoresDto));
        }

        // Store the period closed event
        var closedEvent = new PeriodClosed(
            periodId,
            request.HouseholdId,
            periodStart,
            periodEnd,
            payouts.AsReadOnly(),
            DateTime.UtcNow);

        await store.AppendToStreamAsync(salaryStreamId, closedEvent, ExpectedVersion.Any);

        return Results.Ok(new ClosePeriodResponse(periodId, periodStart, periodEnd, payoutDtos));
    }

    private static (DateTime start, DateTime end) GetLastCompletedPayPeriod(DateTime today, int paydayDay)
    {
        var paydayThisMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        if (today >= paydayThisMonth.Date)
        {
            // We're past payday this month — the period that just ended is: (payday last month + 1 day) → payday this month
            var prevMonth = paydayThisMonth.AddMonths(-1);
            var start = new DateTime(prevMonth.Year, prevMonth.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            return (start, paydayThisMonth);
        }
        else
        {
            // Before payday this month — last completed period ended on payday last month
            var paydayLastMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            var twoMonthsAgo = paydayLastMonth.AddMonths(-1);
            var start = new DateTime(twoMonthsAgo.Year, twoMonthsAgo.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            return (start, paydayLastMonth);
        }
    }

    private List<string> CalculateMissedInstances(
        ChoreCreated chore, 
        DateTime? lastCompleted, 
        DateTime periodStart,
        DateTime periodEnd,
        List<ChoreCompleted>? completionsInPeriod)
    {
        var missed = new List<string>();
        var frequency = chore.Frequency;
        if (frequency == null) return missed;

        var choreStart = chore.StartDate?.Date ?? periodStart;
        var effectiveStart = choreStart > periodStart ? choreStart : periodStart;

        switch (frequency.Type.ToLower())
        {
            case "daily":
                missed = CalculateDailyMissed(effectiveStart, periodEnd, completionsInPeriod);
                break;
            case "weekly":
                missed = CalculateWeeklyMissed(frequency.Days, effectiveStart, periodEnd, completionsInPeriod);
                break;
            case "interval":
                missed = CalculateIntervalMissed(frequency.IntervalDays ?? 1, effectiveStart, periodEnd, lastCompleted, completionsInPeriod);
                break;
        }

        return missed;
    }

    private static List<string> CalculateDailyMissed(DateTime start, DateTime end, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();
        
        for (var date = start; date <= end; date = date.AddDays(1))
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

    private static List<string> CalculateWeeklyMissed(string[]? days, DateTime start, DateTime end, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        if (days == null || days.Length == 0)
        {
            var currentWeek = GetMondayOfWeek(start);
            var endWeek = GetMondayOfWeek(end);
            
            while (currentWeek <= endWeek)
            {
                var weekEnd = currentWeek.AddDays(6);
                var completedThisWeek = completedDates.Any(d => d >= currentWeek && d <= weekEnd);
                
                if (!completedThisWeek)
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
            var requiredDays = days.Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true)).ToHashSet();
            
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (requiredDays.Contains(date.DayOfWeek) && !completedDates.Contains(date))
                {
                    missed.Add(date.ToString("yyyy-MM-dd"));
                }
            }
        }
        
        return missed;
    }

    private static List<string> CalculateIntervalMissed(int intervalDays, DateTime start, DateTime end, DateTime? lastCompleted, List<ChoreCompleted>? completions)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).OrderBy(d => d).ToList() ?? new List<DateTime>();
        
        var lastDue = lastCompleted?.Date ?? start.AddDays(-1);
        var idx = 0;
        
        while (true)
        {
            var nextDue = lastDue.AddDays(intervalDays);
            if (nextDue > end) break;
            
            while (idx < completedDates.Count && completedDates[idx] <= nextDue)
            {
                lastDue = completedDates[idx];
                idx++;
                nextDue = lastDue.AddDays(intervalDays);
                if (nextDue > end) break;
            }
            
            if (nextDue > end) break;
            
            missed.Add(nextDue.ToString("yyyy-MM-dd"));
            lastDue = nextDue;
        }
        
        return missed;
    }
}

internal static class ClosePeriodEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/close-period", async (
            Guid householdId,
            Handler handler) =>
        {
            var command = new ClosePeriodCommand(householdId);
            return await handler.HandleAsync(command);
        });
    }
}
