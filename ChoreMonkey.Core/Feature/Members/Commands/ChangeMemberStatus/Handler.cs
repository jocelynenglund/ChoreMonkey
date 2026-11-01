using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberStatus;

public record ChangeMemberStatusCommand(Guid HouseholdId, Guid MemberId, string Status);
public record ChangeMemberStatusRequest(string Status);
public record ChangeMemberStatusResponse(Guid MemberId, string Status);

internal class Handler(IEventStore store)
{
    public async Task<ChangeMemberStatusResponse?> HandleAsync(ChangeMemberStatusCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var decision = ChangeMemberStatusSlice.Decide(history, request);
        if (!decision.Accepted) return null;

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return new ChangeMemberStatusResponse(request.MemberId, request.Status);
    }
}

internal static class ChangeMemberStatusEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/members/{memberId:guid}/status", async (
            Guid householdId,
            Guid memberId,
            ChangeMemberStatusRequest request,
            Handler handler) =>
        {
            var command = new ChangeMemberStatusCommand(householdId, memberId, request.Status?.Trim() ?? "");
            var result = await handler.HandleAsync(command);
            
            if (result == null)
                return Results.NotFound("Member not found");
                
            return Results.Ok(result);
        });
    }
}
