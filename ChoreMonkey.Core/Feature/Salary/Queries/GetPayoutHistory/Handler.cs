using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetPayoutHistory;

public record GetPayoutHistoryQuery(Guid HouseholdId);

public record PayoutSummaryDto(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal NetPay);

public record PeriodSummaryDto(
    Guid PeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayoutSummaryDto> Payouts);

public record PayoutHistoryResponse(List<PeriodSummaryDto> Periods);

internal class Handler(IEventStore store)
{
    public async Task<PayoutHistoryResponse> HandleAsync(GetPayoutHistoryQuery request)
    {
        var streamId = SalaryAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);

        var periods = events
            .OfType<PeriodClosed>()
            .OrderByDescending(e => e.PeriodEnd)
            .Select(e => new PeriodSummaryDto(
                e.PeriodId,
                e.PeriodStart,
                e.PeriodEnd,
                e.Payouts.Select(p => new PayoutSummaryDto(
                    p.MemberId,
                    p.Name,
                    p.BaseSalary,
                    p.GrossDeductions,
                    p.GrossBonuses,
                    p.NetPay)).ToList()))
            .ToList();

        return new PayoutHistoryResponse(periods);
    }
}

internal static class GetPayoutHistoryEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/history", async (
            Guid householdId,
            Handler handler) =>
        {
            var query = new GetPayoutHistoryQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
