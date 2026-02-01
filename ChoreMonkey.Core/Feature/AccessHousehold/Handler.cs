using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.AccessHousehold;

public record AccessHouseholdQuery(Guid HouseholdId, int PinCode);
public record AccessHouseholdResponse(bool Success, Guid HouseholdId, string? HouseholdName);

internal class Handler(IEventStore store)
{
    public async Task<AccessHouseholdResponse> HandleAsync(AccessHouseholdQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);

        var events = await store.LoadEventsAsync(streamId);
        var householdCreated = events
            .OfType<HouseholdCreated>()
            .FirstOrDefault();

        if (householdCreated == null)
        {
            return new AccessHouseholdResponse(false, request.HouseholdId, null);
        }

        var pinMatches = PinHasher.VerifyPin(request.PinCode, householdCreated.PinHash);

        return new AccessHouseholdResponse(
            pinMatches,
            request.HouseholdId,
            pinMatches ? householdCreated.Name : null
        );
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
        });
    }
}

public record AccessHouseholdRequest(int PinCode);
