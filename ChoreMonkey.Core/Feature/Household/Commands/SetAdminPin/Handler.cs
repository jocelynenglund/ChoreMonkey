using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetAdminPin;

public record SetAdminPinCommand(Guid HouseholdId, int CurrentPinCode, int NewPinCode);
public record SetAdminPinResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(SetAdminPinCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        // The current PIN is verified here; the new one is only hashed once that has passed.
        var verified = HouseholdPins.AdminPinMatches(history, request.CurrentPinCode);
        var newPinHash = verified ? PinHasher.HashPin(request.NewPinCode) : null;

        var decision = SetAdminPinSlice.Decide(history, request, verified, newPinHash);
        if (!decision.Accepted) return (false, false);

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return (true, true);
    }
}

internal static class SetAdminPinEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/admin-pin", 
            async (Guid householdId, SetAdminPinRequest dto, Handler handler) =>
        {
            var command = new SetAdminPinCommand(householdId, dto.CurrentPinCode, dto.NewPinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new SetAdminPinResponse(success));
        });
    }
}

public record SetAdminPinRequest(int CurrentPinCode, int NewPinCode);
