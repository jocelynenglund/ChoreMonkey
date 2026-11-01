using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetAvailablePeriods;

public record GetAvailablePeriodsQuery(Guid HouseholdId);

public record AvailablePeriodDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool IsClosed,
    Guid? PeriodId);       // null if not yet closed

public record AvailablePeriodsResponse(List<AvailablePeriodDto> Periods);

internal class Handler(IEventStore store)
{
    public async Task<AvailablePeriodsResponse> HandleAsync(GetAvailablePeriodsQuery request)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(SalaryAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return AvailablePeriodsView.Project(history, DateTime.UtcNow);
    }
}

internal static class GetAvailablePeriodsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/available-periods", async (
            Guid householdId,
            Handler handler) =>
        {
            var query = new GetAvailablePeriodsQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
