using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Commands.RevokeDisplayLink;

internal class Handler(IEventStore store, DisplayLinkVault vault)
{
    public async Task<IResult> HandleAsync(RevokeDisplayLinkCommand command, HttpContext http, IHouseholdPrincipalAccessor principal)
    {
        var streamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var who = DisplaySupport.CheckAdmin(http, principal, history, command.HouseholdId);
        if (who == DisplaySupport.Who.Nobody && history.OfType<HouseholdCreated>().Any())
            return Results.Json(new { error = "Admin PIN required." }, statusCode: StatusCodes.Status401Unauthorized);

        var decision = RevokeDisplayLinkSlice.Decide(history, command, who == DisplaySupport.Who.Admin);
        if (!decision.Accepted)
            return Results.Json(new { error = decision.Error }, statusCode: decision.Kind.StatusCode());

        foreach (var e in decision.Events.OfType<DisplayLinkRevoked>())
            await store.AppendToStreamAsync(streamId, e, ExpectedVersion.Any);

        // The address stops working at once: its secret is deleted from the vault.
        vault.Remove(command.HouseholdId, command.LinkId);
        return Results.NoContent();
    }
}

internal static class RevokeDisplayLinkEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapDelete("households/{householdId:guid}/display-links/{linkId:guid}", async (
            Guid householdId,
            Guid linkId,
            HttpContext http,
            IHouseholdPrincipalAccessor principal,
            Handler handler) =>
            await handler.HandleAsync(new RevokeDisplayLinkCommand(householdId, linkId), http, principal));
    }
}
