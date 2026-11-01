using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.CompleteChore;

public record CompleteChoreCommand(
    Guid HouseholdId, 
    Guid ChoreId, 
    Guid MemberId,
    DateTime? CompletedAt = null);

public record CompleteChoreRequest(
    Guid MemberId,
    DateTime? CompletedAt = null);

public record CompleteChoreResponse(
    Guid ChoreId, 
    Guid CompletedBy, 
    DateTime CompletedAt);

internal class Handler(IEventStore store)
{
    public async Task<CompleteChoreResponse> HandleAsync(CompleteChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var now = DateTime.UtcNow;

        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var decision = CompleteChoreSlice.Decide(history, request, now);
        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return new CompleteChoreResponse(request.ChoreId, request.MemberId, request.CompletedAt ?? now);
    }
}

internal static class CompleteChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/complete", async (
            Guid householdId,
            Guid choreId,
            CompleteChoreRequest request,
            Handler handler) =>
        {
            var command = new CompleteChoreCommand(
                householdId, 
                choreId, 
                request.MemberId,
                request.CompletedAt);
                
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
