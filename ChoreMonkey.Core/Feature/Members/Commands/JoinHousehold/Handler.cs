using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace ChoreMonkey.Core.Feature.Members.Commands.JoinHousehold;

public record JoinHouseholdCommand(Guid HouseholdId, Guid InviteId, string Nickname);
public record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);

internal class Handler(IEventStore store)
{
    public async Task<JoinHouseholdResponse?> HandleAsync(JoinHouseholdCommand request)
    {
        var streamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var memberId = Guid.NewGuid();
        var decision = JoinHouseholdSlice.Decide(history, request, memberId);
        if (!decision.Accepted) return null;

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

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
            Handler handler,
            HttpResponse response,
            ISessionTokenService tokens,
            IHostEnvironment env) =>
        {
            var command = new JoinHouseholdCommand(householdId, request.InviteId, request.Nickname);
            var result = await handler.HandleAsync(command);

            if (result == null)
                return Results.BadRequest("Invalid invite");

            var principal = new HouseholdPrincipal(result.HouseholdId, result.MemberId, IsAdmin: false);
            SessionCookie.Issue(response, principal, tokens, env);

            return Results.Ok(result);
        })
        .RequireRateLimiting("auth");
    }
}

public record JoinRequest(Guid InviteId, string Nickname);
