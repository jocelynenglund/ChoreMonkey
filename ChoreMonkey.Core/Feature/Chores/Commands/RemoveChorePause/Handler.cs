using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.RemoveChorePause;

public record RemoveChorePauseCommand(Guid HouseholdId, Guid ChoreId, Guid PauseId, int PinCode);
public record RemoveChorePauseRequest(int PinCode);
public record RemoveChorePauseResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(RemoveChorePauseCommand request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>()
            .Concat((await store.FetchEventsAsync(choreStreamId)).Cast<object>())
            .ToList();

        var decision = RemoveChorePauseSlice.Decide(history, request);
        if (decision.Kind == ErrorKind.Forbidden) return (false, false);
        if (!decision.Accepted) return (true, false);

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(choreStreamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return (true, true);
    }
}

internal static class RemoveChorePauseEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/pause/{pauseId:guid}/remove",
            async (Guid householdId, Guid choreId, Guid pauseId, RemoveChorePauseRequest dto, Handler handler) =>
        {
            var command = new RemoveChorePauseCommand(householdId, choreId, pauseId, dto.PinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.StatusCode(403);
            }

            if (!success)
            {
                return Results.NotFound();
            }

            return Results.Ok(new RemoveChorePauseResponse(true));
        });
    }
}
