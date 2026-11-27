using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Commands.MakeDisplayLink;

public record MakeDisplayLinkRequest(string? Label);
public record MadeDisplayLink(Guid LinkId, string Label, string Url);

internal class Handler(IEventStore store, DisplayLinkVault vault, DisplaySettings settings)
{
    public async Task<IResult> HandleAsync(MakeDisplayLinkCommand command, HttpContext http, IHouseholdPrincipalAccessor principal)
    {
        if (!vault.IsConfigured)
            return Results.Json(new { error = DisplaySupport.NotSetUp }, statusCode: StatusCodes.Status503ServiceUnavailable);

        var streamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var who = DisplaySupport.CheckAdmin(http, principal, history, command.HouseholdId);
        if (who == DisplaySupport.Who.Nobody && history.OfType<HouseholdCreated>().Any())
            return Results.Json(new { error = "Admin PIN required." }, statusCode: StatusCodes.Status401Unauthorized);

        var linkId = Guid.NewGuid();
        var decision = MakeDisplayLinkSlice.Decide(history, command, who == DisplaySupport.Who.Admin, linkId);
        if (!decision.Accepted)
            return Results.Json(new { error = decision.Error }, statusCode: decision.Kind.StatusCode());

        // The secret is kept first, so an event never points at a link whose secret was lost.
        var token = DisplayLinkVault.NewToken();
        vault.Save(command.HouseholdId, linkId, token);

        var created = decision.Events.OfType<DisplayLinkCreated>().Single();
        await store.AppendToStreamAsync(streamId, created, ExpectedVersion.Any);

        return Results.Ok(new MadeDisplayLink(linkId, created.Label, settings.LinkUrl(token)));
    }
}

internal static class MakeDisplayLinkEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/display-links", async (
            Guid householdId,
            MakeDisplayLinkRequest? request,
            HttpContext http,
            IHouseholdPrincipalAccessor principal,
            Handler handler) =>
            await handler.HandleAsync(new MakeDisplayLinkCommand(householdId, request?.Label), http, principal));
    }
}
