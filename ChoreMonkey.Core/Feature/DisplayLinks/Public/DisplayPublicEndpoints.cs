using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLately;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLinks;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayToday;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Public;

/// <summary>
/// What a connected screen (barrio) reads with a display link: GET /display/{token}/today, /lately, and the page at
/// /display/{token}. The token is the whole credential. An unknown or revoked token is a plain 404. Every answer is
/// noindex, no-store and no-referrer, and rate limited per token ("display" policy, 60 a minute).
/// </summary>
internal static class DisplayPublicEndpoints
{
    public const string RateLimitPolicy = "display";

    public static IEndpointRouteBuilder MapDisplayLinks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/display").WithTags("display").RequireRateLimiting(RateLimitPolicy);
        group.AddEndpointFilter(async (context, next) =>
        {
            var headers = context.HttpContext.Response.Headers;
            headers["X-Robots-Tag"] = "noindex, nofollow";
            headers["Cache-Control"] = "no-store";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
            return await next(context);
        });

        group.MapGet("{token}/today", async (string token, Resolver resolver, Queries.DisplayToday.Handler today) =>
        {
            var household = await resolver.ResolveAsync(token);
            if (household == null) return Results.NotFound();
            var result = await today.HandleAsync(household.Value);
            return result == null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("{token}/lately", async (string token, Resolver resolver, Queries.DisplayLately.Handler lately) =>
        {
            var household = await resolver.ResolveAsync(token);
            if (household == null) return Results.NotFound();
            var result = await lately.HandleAsync(household.Value);
            return result == null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("{token}", async (string token, Resolver resolver, Queries.DisplayToday.Handler today, Queries.DisplayLately.Handler lately) =>
        {
            var household = await resolver.ResolveAsync(token);
            if (household == null) return Results.NotFound();
            var todayView = await today.HandleAsync(household.Value);
            var latelyView = await lately.HandleAsync(household.Value);
            if (todayView == null || latelyView == null) return Results.NotFound();
            return Results.Content(DisplayPage.Render(todayView, latelyView), "text/html; charset=utf-8");
        });

        return app;
    }

    /// <summary>Token to household: the vault knows the token, and the household's events must still call the link live.</summary>
    internal class Resolver(DisplayLinkVault vault, IEventStore store)
    {
        public async Task<Guid?> ResolveAsync(string token)
        {
            var found = vault.Resolve(token);
            if (found == null) return null;

            var history = (await store.FetchEventsAsync(Domain.HouseholdAggregate.StreamId(found.Value.HouseholdId))).Cast<object>().ToList();
            return DisplayLinksView.Project(history).Links.Any(l => l.LinkId == found.Value.LinkId) ? found.Value.HouseholdId : null;
        }
    }
}
