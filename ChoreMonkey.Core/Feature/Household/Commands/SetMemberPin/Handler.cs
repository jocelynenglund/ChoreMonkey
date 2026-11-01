using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetMemberPin;

public record SetMemberPinCommand(Guid HouseholdId, int AdminPinCode, int MemberPinCode);
public record SetMemberPinResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(SetMemberPinCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        // The admin PIN is verified here; the new member PIN is only hashed once that has passed.
        var verified = HouseholdPins.AdminPinMatches(history, request.AdminPinCode);
        var memberPinHash = verified ? PinHasher.HashPin(request.MemberPinCode) : null;

        var decision = SetMemberPinSlice.Decide(history, request, verified, memberPinHash);
        if (!decision.Accepted) return (false, false);

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return (true, true);
    }
}

internal static class SetMemberPinEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/member-pin", 
            async (Guid householdId, SetMemberPinRequest dto, Handler handler) =>
        {
            var command = new SetMemberPinCommand(householdId, dto.AdminPinCode, dto.MemberPinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new SetMemberPinResponse(success));
        });
    }
}

public record SetMemberPinRequest(int AdminPinCode, int MemberPinCode);
