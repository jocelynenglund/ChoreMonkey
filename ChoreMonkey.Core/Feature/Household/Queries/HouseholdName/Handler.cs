using ChoreMonkey.Core.Domain;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChoreMonkey.Core.Feature.Household.Queries.HouseholdName;

public record HouseholdNameQuery(Guid HouseholdId);
public record HouseholdNameResponse(Guid HouseholdId, string HouseholdName);

internal class Handler(IEventStore store)
{
    public async Task<HouseholdNameResponse> HandleAsync(HouseholdNameQuery request)
    {
        // Placeholder implementation
        var streamId  = HouseholdAggregate.StreamId(request.HouseholdId);

        var @events = (await store.FetchEventsAsync(streamId))
            .OfType<ChoreMonkey.Events.HouseholdCreated>()
            .FirstOrDefault();
        return new HouseholdNameResponse(request.HouseholdId, @events?.Name ?? "Unknown");
    }
}
internal static class HouseholdNameEndpoint
{

    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}", async (Guid householdId, Feature.Household.Queries.HouseholdName.Handler handler) =>
        {
            var query = new HouseholdNameQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
