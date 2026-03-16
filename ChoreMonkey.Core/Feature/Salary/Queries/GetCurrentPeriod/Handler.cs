using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetCurrentPeriod;

public record GetCurrentPeriodQuery(Guid HouseholdId, Guid? MemberId = null);

public record MemberPeriodSummary(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal Projected);

public record CurrentPeriodResponse(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<MemberPeriodSummary> Members);

internal class Handler(IEventStore store)
{
    public async Task<CurrentPeriodResponse> HandleAsync(GetCurrentPeriodQuery request)
    {
        // Get period boundaries (current month)
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var currentPeriodPrefix = periodStart.ToString("yyyy-MM");

        // Fetch all relevant streams
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var salaryEvents = await store.FetchEventsAsync(salaryStreamId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Get member names
        var memberNames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(e => e.MemberId, e => e.Nickname);

        // Get latest salary config per member
        var salaryConfigs = salaryEvents
            .OfType<MemberSalarySet>()
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get chore rates
        var choreRates = salaryEvents
            .OfType<ChoreRatesSet>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get chore info (for names and type)
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        var chores = choreEvents
            .OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);

        // Get missed chores in period (Period string starts with current month)
        var missedChores = choreEvents
            .OfType<ChoreMissedAcknowledged>()
            .Where(e => e.Period.StartsWith(currentPeriodPrefix))
            .ToList();

        // Get completed bonus chores in period
        var completedChores = choreEvents
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt <= periodEnd)
            .ToList();

        // Calculate per-member summaries
        var members = new List<MemberPeriodSummary>();
        var allMemberIds = memberNames.Keys.ToList();

        // Filter by memberId if specified
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

            // Calculate deductions from missed chores
            decimal totalDeductions = 0m;
            foreach (var missed in missedChores.Where(m => m.MemberId == memberId))
            {
                var rates = choreRates.GetValueOrDefault(missed.ChoreId);
                var deductionRate = rates?.DeductionRate ?? 0m;
                totalDeductions += deductionRate * deductionMultiplier;
            }

            // Calculate bonuses from completed optional chores
            decimal totalBonuses = 0m;
            foreach (var completed in completedChores.Where(c => c.CompletedByMemberId == memberId))
            {
                var chore = chores.GetValueOrDefault(completed.ChoreId);
                if (chore?.IsOptional == true)
                {
                    var rates = choreRates.GetValueOrDefault(completed.ChoreId);
                    var bonusRate = rates?.BonusRate ?? 0m;
                    totalBonuses += bonusRate * bonusMultiplier;
                }
            }

            var projected = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);

            members.Add(new MemberPeriodSummary(
                memberId,
                memberNames.GetValueOrDefault(memberId, "Unknown"),
                baseSalary,
                totalDeductions,
                totalBonuses,
                projected));
        }

        return new CurrentPeriodResponse(periodStart, periodEnd, members);
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
