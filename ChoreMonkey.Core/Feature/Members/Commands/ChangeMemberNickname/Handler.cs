using ItsyBIT.Decisions;
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
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var decision = ChangeMemberNicknameSlice.Decide(history, request);
        if (!decision.Accepted) return null;

        // Asking for the nickname the member already has records nothing and still answers with it.
        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

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
