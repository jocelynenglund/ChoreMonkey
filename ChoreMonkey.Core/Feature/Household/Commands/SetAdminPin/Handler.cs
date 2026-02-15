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
        var events = await store.FetchEventsAsync(streamId);
        
        var householdCreated = events
            .OfType<HouseholdCreated>()
            .FirstOrDefault();

        if (householdCreated == null)
        {
            return (false, false);
        }

        // Check for existing admin PIN changes
        var adminPinChanged = events
            .OfType<AdminPinChanged>()
            .LastOrDefault();
        var currentAdminPinHash = adminPinChanged?.NewPinHash ?? householdCreated.PinHash;

        // Verify current admin PIN
        var isAdmin = PinHasher.VerifyPin(request.CurrentPinCode, currentAdminPinHash);
        if (!isAdmin)
        {
            return (false, false);
        }

        // Set new admin PIN
        var newPinHash = PinHasher.HashPin(request.NewPinCode);
        var changeEvent = new AdminPinChanged(request.HouseholdId, newPinHash);
        await store.AppendToStreamAsync(streamId, changeEvent, ExpectedVersion.Any);

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
