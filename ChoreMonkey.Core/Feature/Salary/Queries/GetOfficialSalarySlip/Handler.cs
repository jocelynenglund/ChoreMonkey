using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetOfficialSalarySlip;

public record GetOfficialSalarySlipQuery(Guid HouseholdId, Guid PeriodId, Guid MemberId);

public record DeductionLineDto(
    string ChoreName,
    decimal BaseRate,
    decimal Multiplier,
    decimal Amount);

public record BonusLineDto(
    string ChoreName,
    decimal BaseRate,
    decimal Multiplier,
    decimal Amount);

public record OfficialSalarySlipResponse(
    Guid PeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string MemberName,
    decimal BaseSalary,
    List<DeductionLineDto> Deductions,
    List<BonusLineDto> Bonuses,
    decimal GrossDeductions,
    decimal GrossBonuses,
    decimal NetPay);

internal class Handler(IEventStore store)
{
    public async Task<OfficialSalarySlipResponse?> HandleAsync(GetOfficialSalarySlipQuery request)
    {
        var streamId = SalaryAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);

        var period = events
            .OfType<PeriodClosed>()
            .FirstOrDefault(e => e.PeriodId == request.PeriodId);

        if (period is null)
            return null;

        var payout = period.Payouts
            .FirstOrDefault(p => p.MemberId == request.MemberId);

        if (payout is null)
            return null;

        return new OfficialSalarySlipResponse(
            period.PeriodId,
            period.PeriodStart,
            period.PeriodEnd,
            payout.Name,
            payout.BaseSalary,
            payout.Deductions.Select(d => new DeductionLineDto(
                d.ChoreName,
                d.BaseRate,
                d.Multiplier,
                d.Amount)).ToList(),
            payout.Bonuses.Select(b => new BonusLineDto(
                b.ChoreName,
                b.BaseRate,
                b.Multiplier,
                b.Amount)).ToList(),
            payout.GrossDeductions,
            payout.GrossBonuses,
            payout.NetPay);
    }
}

internal static class GetOfficialSalarySlipEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/periods/{periodId:guid}/slip/{memberId:guid}", async (
            Guid householdId,
            Guid periodId,
            Guid memberId,
            Handler handler) =>
        {
            var query = new GetOfficialSalarySlipQuery(householdId, periodId, memberId);
            var result = await handler.HandleAsync(query);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
