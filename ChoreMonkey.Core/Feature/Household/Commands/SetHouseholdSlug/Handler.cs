using ItsyBIT.Decisions;
using System.Text.RegularExpressions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetHouseholdSlug;

public record SetHouseholdSlugCommand(Guid HouseholdId, string Slug, string PinCode);

public record SetHouseholdSlugRequest(string Slug);

public record SetHouseholdSlugResponse(string Slug, string Url);

internal class Handler(IEventStore store)
{
    public async Task<(SetHouseholdSlugResponse? Result, string? Error, int StatusCode)> HandleAsync(SetHouseholdSlugCommand command)
    {
        var slug = SetHouseholdSlugSlice.Normalize(command.Slug);
        var householdStreamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var slugStreamId = $"household-slug-{slug}";

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>().ToList();

        // Only a well-formed, unreserved slug may name a stream, and only then is the (slow) PIN check made;
        // Decide re-checks the slug and answers the refusal.
        var verified = false;
        if (SetHouseholdSlugSlice.IsAcceptable(slug))
        {
            verified = int.TryParse(command.PinCode, out var pin) && HouseholdPins.AdminPinMatches(history, pin);
            history.AddRange((await store.FetchEventsAsync(slugStreamId)).Cast<object>());
        }

        var decision = SetHouseholdSlugSlice.Decide(history, command, verified, DateTime.UtcNow);
        if (!decision.Accepted)
            return (null, decision.Error, decision.Kind.StatusCode());

        // SlugClaimed goes to the slug stream first, then HouseholdSlugSet to the household stream.
        foreach (var e in decision.Events.OfType<SlugClaimed>())
            await store.AppendToStreamAsync(slugStreamId, e, ExpectedVersion.Any);
        foreach (var e in decision.Events.OfType<HouseholdSlugSet>())
            await store.AppendToStreamAsync(householdStreamId, e, ExpectedVersion.Any);

        return (new SetHouseholdSlugResponse(slug, $"/h/{slug}"), null, 200);
    }
}

internal static class SetHouseholdSlugEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPut("households/{householdId:guid}/slug", async (
            Guid householdId,
            SetHouseholdSlugRequest request,
            HttpContext httpContext,
            Handler handler) =>
        {
            var pinCode = httpContext.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCode))
                return Results.Json(new { error = "Admin PIN required." }, statusCode: StatusCodes.Status401Unauthorized);

            var command = new SetHouseholdSlugCommand(householdId, request.Slug, pinCode);
            var (result, error, statusCode) = await handler.HandleAsync(command);

            if (result != null)
                return Results.Ok(result);

            return Results.Json(new { error }, statusCode: statusCode);
        });
    }
}
