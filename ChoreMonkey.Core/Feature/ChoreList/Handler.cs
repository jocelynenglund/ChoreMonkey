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

public record ChoreDto(
    Guid ChoreId, 
    string DisplayName, 
    string Description, 
    Guid? AssignedTo,
    FrequencyDto? Frequency = null,
    DateTime? LastCompletedAt = null,
    Guid? LastCompletedBy = null);

public record FrequencyDto(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

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

        // Get latest completion for each chore
        var lastCompletions = events.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CompletedAt).First());

        var chores = createdChores
            .Select(e => {
                var lastCompletion = lastCompletions.GetValueOrDefault(e.ChoreId);
                var frequency = e.Frequency != null 
                    ? new FrequencyDto(e.Frequency.Type, e.Frequency.Days, e.Frequency.IntervalDays)
                    : new FrequencyDto("once");
                    
                return new ChoreDto(
                    e.ChoreId, 
                    e.DisplayName, 
                    e.Description,
                    assignments.GetValueOrDefault(e.ChoreId),
                    frequency,
                    lastCompletion?.CompletedAt,
                    lastCompletion?.CompletedByMemberId);
            })
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
