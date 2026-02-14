using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.SetMemberPin;

public record SetMemberPinCommand(Guid HouseholdId, int AdminPinCode, int MemberPinCode);
public record SetMemberPinResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(SetMemberPinCommand request)
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

        // Verify admin PIN
        var isAdmin = PinHasher.VerifyPin(request.AdminPinCode, currentAdminPinHash);
        if (!isAdmin)
        {
            return (false, false);
        }

        // Set new member PIN
        var memberPinHash = PinHasher.HashPin(request.MemberPinCode);
        var changeEvent = new MemberPinChanged(request.HouseholdId, memberPinHash);
        await store.AppendToStreamAsync(streamId, changeEvent, ExpectedVersion.Any);

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
