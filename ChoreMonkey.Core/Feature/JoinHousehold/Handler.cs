using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.JoinHousehold;

public record JoinHouseholdCommand(Guid HouseholdId, Guid InviteId, string Nickname);
public record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);

internal class Handler(IEventStore store)
{
    public async Task<JoinHouseholdResponse?> HandleAsync(JoinHouseholdCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        // Verify invite exists and belongs to this household
        var invite = events.OfType<InviteGenerated>()
            .FirstOrDefault(e => e.InviteId == request.InviteId);
        
        if (invite == null)
            return null;
        
        // Create new member
        var memberId = Guid.NewGuid();
        var memberJoinedEvent = new MemberJoinedHousehold(
            memberId,
            request.HouseholdId,
            request.InviteId,
            request.Nickname
        );
        
        await store.AppendToStreamAsync(streamId, memberJoinedEvent, ExpectedVersion.Any);
        
        return new JoinHouseholdResponse(memberId, request.HouseholdId, request.Nickname);
    }
}

internal static class JoinHouseholdEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/join", async (
            Guid householdId,
            JoinRequest request,
            Handler handler) =>
        {
            var command = new JoinHouseholdCommand(householdId, request.InviteId, request.Nickname);
            var result = await handler.HandleAsync(command);
            
            if (result == null)
                return Results.BadRequest("Invalid invite");
                
            return Results.Ok(result);
        });
    }
}

public record JoinRequest(Guid InviteId, string Nickname);
