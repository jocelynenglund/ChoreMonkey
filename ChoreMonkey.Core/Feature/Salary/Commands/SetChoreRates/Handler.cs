using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetChoreRates;

public record SetChoreRatesCommand(
    Guid HouseholdId,
    Guid ChoreId,
    decimal? DeductionRate,
    decimal? BonusRate);

public record SetChoreRatesRequest(
    decimal? DeductionRate,
    decimal? BonusRate);

public record SetChoreRatesResponse(
    Guid ChoreId,
    decimal? DeductionRate,
    decimal? BonusRate);

internal class Handler(IEventStore store)
{
    public async Task<SetChoreRatesResponse> HandleAsync(SetChoreRatesCommand request)
    {
        var streamId = SalaryAggregate.StreamId(request.HouseholdId);
        
        var ratesEvent = new ChoreRatesSet(
            request.HouseholdId,
            request.ChoreId,
            request.DeductionRate,
            request.BonusRate,
            DateTime.UtcNow);
            
        await store.AppendToStreamAsync(streamId, ratesEvent, ExpectedVersion.Any);
        
        return new SetChoreRatesResponse(
            request.ChoreId,
            request.DeductionRate,
            request.BonusRate);
    }
}

internal static class SetChoreRatesEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/rates", async (
            Guid householdId,
            Guid choreId,
            SetChoreRatesRequest request,
            Handler handler) =>
        {
            var command = new SetChoreRatesCommand(
                householdId,
                choreId,
                request.DeductionRate,
                request.BonusRate);
                
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
