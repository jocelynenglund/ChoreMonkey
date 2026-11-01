using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.DeleteChore;

public record DeleteChoreCommand(Guid HouseholdId, Guid ChoreId, int PinCode);
public record DeleteChoreResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(DeleteChoreCommand request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>()
            .Concat((await store.FetchEventsAsync(choreStreamId)).Cast<object>())
            .ToList();

        var decision = DeleteChoreSlice.Decide(history, request);
        if (decision.Kind == ErrorKind.Forbidden) return (false, false);
        if (!decision.Accepted) return (true, false);

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(choreStreamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return (true, true); // also when it was deleted already
    }
}

internal static class DeleteChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/delete", 
            async (Guid householdId, Guid choreId, DeleteChoreRequest dto, Handler handler) =>
        {
            var command = new DeleteChoreCommand(householdId, choreId, dto.PinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.StatusCode(403); // Forbidden - not admin
            }

            if (!success)
            {
                return Results.NotFound();
            }

            return Results.Ok(new DeleteChoreResponse(true));
        });
    }
}

public record DeleteChoreRequest(int PinCode);
