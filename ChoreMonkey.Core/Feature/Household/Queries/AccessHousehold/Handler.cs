using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace ChoreMonkey.Core.Feature.Household.Queries.AccessHousehold;

public record AccessHouseholdQuery(Guid HouseholdId, int PinCode);
public record AccessHouseholdResponse(bool Success, Guid HouseholdId, string? HouseholdName, bool IsAdmin = false);

internal class Handler(IEventStore store)
{
    public async Task<AccessHouseholdResponse> HandleAsync(AccessHouseholdQuery request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        // The PIN check stays here, in the adapter; the view decides which hashes to try, admin first.
        return AccessHouseholdView.Project(history, request.HouseholdId, hash => PinHasher.VerifyPin(request.PinCode, hash));
    }
}

internal static class AccessHouseholdEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/access", async (
            Guid householdId,
            AccessHouseholdRequest dto,
            Handler handler,
            HttpResponse response,
            ISessionTokenService tokens,
            IHostEnvironment env) =>
        {
            var query = new AccessHouseholdQuery(householdId, dto.PinCode);
            var result = await handler.HandleAsync(query);

            if (!result.Success)
            {
                return Results.Unauthorized();
            }

            // AccessHousehold can't disambiguate which member a member-PIN belongs to,
            // so MemberId is null here. JoinHousehold (invite flow) issues a cookie
            // with a real MemberId.
            var principal = new HouseholdPrincipal(result.HouseholdId, MemberId: null, result.IsAdmin);
            SessionCookie.Issue(response, principal, tokens, env);

            return Results.Ok(result);
        })
        .RequireRateLimiting("auth");
    }
}

public record AccessHouseholdRequest(int PinCode);
