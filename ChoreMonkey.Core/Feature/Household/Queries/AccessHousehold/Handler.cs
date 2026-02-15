using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Household.Queries.AccessHousehold;

public record AccessHouseholdQuery(Guid HouseholdId, int PinCode);
public record AccessHouseholdResponse(bool Success, Guid HouseholdId, string? HouseholdName, bool IsAdmin = false);

internal class Handler(IEventStore store)
{
    public async Task<AccessHouseholdResponse> HandleAsync(AccessHouseholdQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);

        var events = await store.FetchEventsAsync(streamId);
        var householdCreated = events
            .OfType<HouseholdCreated>()
            .FirstOrDefault();

        if (householdCreated == null)
        {
            return new AccessHouseholdResponse(false, request.HouseholdId, null);
        }

        // Check for admin PIN changes
        var adminPinChanged = events
            .OfType<AdminPinChanged>()
            .LastOrDefault();
        var currentAdminPinHash = adminPinChanged?.NewPinHash ?? householdCreated.PinHash;

        // Check admin PIN first
        var isAdmin = PinHasher.VerifyPin(request.PinCode, currentAdminPinHash);
        if (isAdmin)
        {
            return new AccessHouseholdResponse(true, request.HouseholdId, householdCreated.Name, IsAdmin: true);
        }

        // Check member PIN if set (could be from creation or later change)
        var memberPinChanged = events
            .OfType<MemberPinChanged>()
            .LastOrDefault();
        var currentMemberPinHash = memberPinChanged?.NewMemberPinHash ?? householdCreated.MemberPinHash;
        
        if (currentMemberPinHash != null)
        {
            var isMember = PinHasher.VerifyPin(request.PinCode, currentMemberPinHash);
            if (isMember)
            {
                return new AccessHouseholdResponse(true, request.HouseholdId, householdCreated.Name, IsAdmin: false);
            }
        }

        // No match
        return new AccessHouseholdResponse(false, request.HouseholdId, null);
    }
}

internal static class AccessHouseholdEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/access", async (Guid householdId, AccessHouseholdRequest dto, Handler handler) =>
        {
            var query = new AccessHouseholdQuery(householdId, dto.PinCode);
            var result = await handler.HandleAsync(query);

            if (!result.Success)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(result);
        })
        .RequireRateLimiting("auth");
    }
}

public record AccessHouseholdRequest(int PinCode);
