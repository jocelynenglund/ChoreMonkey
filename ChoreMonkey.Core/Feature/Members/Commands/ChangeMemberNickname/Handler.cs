using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberNickname;

public record ChangeMemberNicknameCommand(Guid HouseholdId, Guid MemberId, string NewNickname);
public record ChangeMemberNicknameRequest(string Nickname);
public record ChangeMemberNicknameResponse(Guid MemberId, string Nickname);

internal class Handler(IEventStore store)
{
    public async Task<ChangeMemberNicknameResponse?> HandleAsync(ChangeMemberNicknameCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        // Find the member and their current nickname
        var memberJoined = events.OfType<MemberJoinedHousehold>()
            .FirstOrDefault(m => m.MemberId == request.MemberId);
        
        if (memberJoined == null)
            return null;
        
        // Get current nickname (could have been changed)
        var currentNickname = events.OfType<MemberNicknameChanged>()
            .Where(m => m.MemberId == request.MemberId)
            .OrderByDescending(m => m.TimestampUtc)
            .Select(m => m.NewNickname)
            .FirstOrDefault() ?? memberJoined.Nickname;
        
        // Don't create event if nickname hasn't changed
        if (currentNickname == request.NewNickname)
            return new ChangeMemberNicknameResponse(request.MemberId, currentNickname);
        
        var nicknameChangedEvent = new MemberNicknameChanged(
            request.MemberId,
            request.HouseholdId,
            currentNickname,
            request.NewNickname
        );
        
        await store.AppendToStreamAsync(streamId, nicknameChangedEvent, ExpectedVersion.Any);
        
        return new ChangeMemberNicknameResponse(request.MemberId, request.NewNickname);
    }
}

internal static class ChangeMemberNicknameEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/members/{memberId:guid}/nickname", async (
            Guid householdId,
            Guid memberId,
            ChangeMemberNicknameRequest request,
            Handler handler) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nickname))
                return Results.BadRequest("Nickname is required");
            
            var command = new ChangeMemberNicknameCommand(householdId, memberId, request.Nickname.Trim());
            var result = await handler.HandleAsync(command);
            
            if (result == null)
                return Results.NotFound("Member not found");
                
            return Results.Ok(result);
        });
    }
}
