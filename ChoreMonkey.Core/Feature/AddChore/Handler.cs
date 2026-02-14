using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.AddChore;

public record AddChoreCommand(Guid HouseholdId, Guid ChoreId, string DisplayName, string Description);
public record AddChoreRequest(string DisplayName, string Description);

internal class Handler(IEventStore store)
{
    public async Task HandleAsync(AddChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        await store.AppendToStreamAsync(streamId, new ChoreCreated(request.ChoreId, request.HouseholdId, request.DisplayName, request.Description), ExpectedVersion.Any);
    }
}

internal static class AddChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores", async (Guid householdId, AddChoreRequest dto, Feature.AddChore.Handler handler) =>
        {
            var command = new AddChoreCommand(householdId, Guid.NewGuid(), dto.DisplayName, dto.Description);
            await handler.HandleAsync(command);
            return Results.Created($"/api/households/{householdId}/chores", null);
        });
    }
}
