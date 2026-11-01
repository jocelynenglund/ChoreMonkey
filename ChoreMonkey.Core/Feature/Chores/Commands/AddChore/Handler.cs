using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.Chores.Commands.AddChore;

public record AddChoreCommand(
    Guid HouseholdId, 
    Guid ChoreId, 
    string DisplayName, 
    string Description,
    ChoreFrequency? Frequency = null,
    bool IsOptional = false,
    DateTime? StartDate = null,
    bool IsRequired = true,
    decimal MissedDeduction = 10m);

public record AddChoreRequest(
    string DisplayName,
    string Description,
    FrequencyRequest? Frequency = null,
    bool IsOptional = false,
    DateTime? StartDate = null,
    bool IsRequired = true,
    decimal MissedDeduction = 10m,
    Guid? ChoreId = null);

public record FrequencyRequest(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

public record AddChoreResponse(Guid Id);

internal class Handler(IEventStore store)
{
    public async Task<AddChoreResponse> HandleAsync(AddChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);

        var decision = AddChoreSlice.Decide([], request);
        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return new AddChoreResponse(request.ChoreId);
    }
}

internal static class AddChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores", async (Guid householdId, AddChoreRequest dto, Feature.Chores.Commands.AddChore.Handler handler) =>
        {
            var frequency = dto.Frequency != null 
                ? new ChoreFrequency(dto.Frequency.Type, dto.Frequency.Days, dto.Frequency.IntervalDays)
                : null;
                
            var choreId = dto.ChoreId ?? Guid.NewGuid();
            var command = new AddChoreCommand(
                householdId, 
                choreId, 
                dto.DisplayName, 
                dto.Description,
                frequency,
                dto.IsOptional,
                dto.StartDate,
                dto.IsRequired,
                dto.MissedDeduction);
                
            var result = await handler.HandleAsync(command);
            return Results.Created($"/api/households/{householdId}/chores/{choreId}", result);
        });
    }
}
