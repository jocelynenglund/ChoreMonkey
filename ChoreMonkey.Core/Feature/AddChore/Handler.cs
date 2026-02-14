using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.AddChore;

public record AddChoreCommand(
    Guid HouseholdId, 
    Guid ChoreId, 
    string DisplayName, 
    string Description,
    ChoreFrequency? Frequency = null,
    bool IsOptional = false);

public record AddChoreRequest(
    string DisplayName, 
    string Description,
    FrequencyRequest? Frequency = null,
    bool IsOptional = false);

public record FrequencyRequest(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

internal class Handler(IEventStore store)
{
    public async Task HandleAsync(AddChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        // Default to "once" if no frequency specified
        var frequency = request.Frequency ?? new ChoreFrequency("once");
        
        var choreCreated = new ChoreCreated(
            request.ChoreId, 
            request.HouseholdId, 
            request.DisplayName, 
            request.Description,
            frequency,
            request.IsOptional);
            
        await store.AppendToStreamAsync(streamId, choreCreated, ExpectedVersion.Any);
    }
}

internal static class AddChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores", async (Guid householdId, AddChoreRequest dto, Feature.AddChore.Handler handler) =>
        {
            var frequency = dto.Frequency != null 
                ? new ChoreFrequency(dto.Frequency.Type, dto.Frequency.Days, dto.Frequency.IntervalDays)
                : null;
                
            var command = new AddChoreCommand(
                householdId, 
                Guid.NewGuid(), 
                dto.DisplayName, 
                dto.Description,
                frequency,
                dto.IsOptional);
                
            await handler.HandleAsync(command);
            return Results.Created($"/api/households/{householdId}/chores", null);
        });
    }
}
