using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLinks;

/// <summary>A live link with its address; the address is null only if its secret can no longer be unsealed.</summary>
public record DisplayLinkListed(Guid LinkId, string Label, string CreatedAt, string? Url);
public record DisplayLinksListed(List<DisplayLinkListed> Links);

internal class Handler(IEventStore store, DisplayLinkVault vault, DisplaySettings settings)
{
    public async Task<IResult> HandleAsync(Guid householdId, HttpContext http, IHouseholdPrincipalAccessor principal)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(householdId))).Cast<object>().ToList();
        if (!history.OfType<HouseholdCreated>().Any()) return Results.NotFound(new { error = "Household not found" });

        switch (DisplaySupport.CheckAdmin(http, principal, history, householdId))
        {
            case DisplaySupport.Who.Nobody: return Results.Json(new { error = "Admin PIN required." }, statusCode: StatusCodes.Status401Unauthorized);
            case DisplaySupport.Who.NotAdmin: return Results.Json(new { error = "Admin access required" }, statusCode: StatusCodes.Status403Forbidden);
        }

        var links = DisplayLinksView.Project(history).Links
            .Select(l =>
            {
                var token = vault.Reveal(householdId, l.LinkId);
                return new DisplayLinkListed(l.LinkId, l.Label, l.CreatedAt, token == null ? null : settings.LinkUrl(token));
            })
            .ToList();
        return Results.Ok(new DisplayLinksListed(links));
    }
}

internal static class DisplayLinksEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/display-links", async (
            Guid householdId,
            HttpContext http,
            IHouseholdPrincipalAccessor principal,
            Handler handler) =>
            await handler.HandleAsync(householdId, http, principal));
    }
}
