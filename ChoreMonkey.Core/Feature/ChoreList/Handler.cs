using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace ChoreMonkey.Core.Feature.ChoreList;

public record GetChoresQuery(Guid HouseholdId);
public record ChoreDto(Guid ChoreId, string DisplayName, string Description, Guid? AssignedTo);
public record GetChoresResponse(List<ChoreDto> Chores);

internal class Handler(IEventStore store)
{
    public async Task<GetChoresResponse> HandleAsync(GetChoresQuery request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);

        // Get all created chores
        var createdChores = events.OfType<ChoreCreated>().ToList();
        
        // Get latest assignment for each chore
        var assignments = events.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last().AssignedToMemberId);

        var chores = createdChores
            .Select(e => new ChoreDto(
                e.ChoreId, 
                e.DisplayName, 
                e.Description,
                assignments.GetValueOrDefault(e.ChoreId)))
            .ToList();

        return new GetChoresResponse(chores);
    }
}

internal static class ChoreListEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/chores", async (Guid householdId, Feature.ChoreList.Handler handler) =>
        {
            var query = new GetChoresQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
