using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.ClosePeriod;

public record ClosePeriodCommand(Guid HouseholdId, DateTime PeriodEnd);

public record ClosePeriodRequest(DateTime PeriodEnd);

public record PayoutSummaryDto(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal NetPay);

public record ClosePeriodResponse(
    Guid PeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayoutSummaryDto> Payouts);

internal class Handler(IEventStore store)
{
    public async Task<ClosePeriodResponse> HandleAsync(ClosePeriodCommand request)
    {
        var periodId = Guid.NewGuid();
        var periodEnd = request.PeriodEnd.Date;
        var periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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

        // Get chore info
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        var chores = choreEvents
            .OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);

        // Get missed and completed chores in period
        var missedChores = choreEvents
            .OfType<ChoreMissedAcknowledged>()
            .Where(e => e.Period.StartsWith(currentPeriodPrefix))
            .ToList();

        var completedChores = choreEvents
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt <= periodEnd.AddDays(1))
            .ToList();

        // Calculate payouts
        var payouts = new List<PeriodPayout>();
        var payoutDtos = new List<PayoutSummaryDto>();

        foreach (var memberId in memberNames.Keys)
        {
            var config = salaryConfigs.GetValueOrDefault(memberId);
            var baseSalary = config?.BaseSalary ?? 0m;
            var deductionMultiplier = config?.DeductionMultiplier ?? 1.0m;
            var bonusMultiplier = config?.BonusMultiplier ?? 1.0m;

            var deductions = new List<PayoutDeduction>();
            var bonuses = new List<PayoutBonus>();
            decimal totalDeductions = 0m;
            decimal totalBonuses = 0m;

            // Calculate deductions
            foreach (var missed in missedChores.Where(m => m.MemberId == memberId))
            {
                var rates = choreRates.GetValueOrDefault(missed.ChoreId);
                var chore = chores.GetValueOrDefault(missed.ChoreId);
                var deductionRate = rates?.DeductionRate ?? 0m;
                var amount = deductionRate * deductionMultiplier;
                
                deductions.Add(new PayoutDeduction(
                    missed.ChoreId,
                    chore?.DisplayName ?? "Unknown",
                    deductionRate,
                    deductionMultiplier,
                    amount));
                totalDeductions += amount;
            }

            // Calculate bonuses
            foreach (var completed in completedChores.Where(c => c.CompletedByMemberId == memberId))
            {
                var chore = chores.GetValueOrDefault(completed.ChoreId);
                if (chore?.IsOptional == true)
                {
                    var rates = choreRates.GetValueOrDefault(completed.ChoreId);
                    var bonusRate = rates?.BonusRate ?? 0m;
                    var amount = bonusRate * bonusMultiplier;
                    
                    bonuses.Add(new PayoutBonus(
                        completed.ChoreId,
                        chore.DisplayName,
                        bonusRate,
                        bonusMultiplier,
                        amount));
                    totalBonuses += amount;
                }
            }

            var netPay = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);
            var memberName = memberNames.GetValueOrDefault(memberId, "Unknown");

            payouts.Add(new PeriodPayout(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay,
                deductions,
                bonuses));

            payoutDtos.Add(new PayoutSummaryDto(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay));
        }

        // Store the period closed event
        var closedEvent = new PeriodClosed(
            periodId,
            request.HouseholdId,
            periodStart,
            periodEnd,
            payouts,
            DateTime.UtcNow);

        await store.AppendToStreamAsync(salaryStreamId, closedEvent, ExpectedVersion.Any);

        return new ClosePeriodResponse(periodId, periodStart, periodEnd, payoutDtos);
    }
}

internal static class ClosePeriodEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/close-period", async (
            Guid householdId,
            ClosePeriodRequest request,
            Handler handler) =>
        {
            var command = new ClosePeriodCommand(householdId, request.PeriodEnd);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
