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
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();
        return OfficialSalarySlipView.Project(history, request.PeriodId, request.MemberId);
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
