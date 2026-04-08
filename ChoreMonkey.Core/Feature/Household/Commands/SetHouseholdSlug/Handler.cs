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
    private static readonly Regex SlugPattern = new(@"^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$", RegexOptions.Compiled);

    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "h", "health", "household", "households",
        "invite", "join", "login", "logout", "register", "settings",
        "help", "about", "terms", "privacy"
    };

    public async Task<(SetHouseholdSlugResponse? Result, string? Error, int StatusCode)> HandleAsync(SetHouseholdSlugCommand command)
    {
        var slug = command.Slug.Trim().ToLowerInvariant();

        // Validate format
        if (!SlugPattern.IsMatch(slug))
            return (null, "Slug must be 3-30 characters, lowercase letters/numbers/hyphens, starting and ending with a letter or number.", 400);

        // Check blocklist
        if (Blocklist.Contains(slug))
            return (null, "This slug is reserved and cannot be used.", 400);

        // Verify admin PIN
        var householdStreamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);

        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
            return (null, "Household not found.", 404);

        var adminPinChanged = householdEvents.OfType<AdminPinChanged>().LastOrDefault();
        var currentPinHash = adminPinChanged?.NewPinHash ?? householdCreated.PinHash;

        if (!int.TryParse(command.PinCode, out var pin) || !PinHasher.VerifyPin(pin, currentPinHash))
            return (null, "Invalid admin PIN.", 403);

        // Check uniqueness: does stream household-slug-{slug} already have events?
        var slugStreamId = $"household-slug-{slug}";
        var existingSlugEvents = await store.FetchEventsAsync(slugStreamId);

        if (existingSlugEvents.Any())
        {
            // Allow re-claiming if it's the same household
            var existingClaim = existingSlugEvents.OfType<SlugClaimed>().FirstOrDefault();
            if (existingClaim?.HouseholdId != command.HouseholdId)
                return (null, "This slug is already taken.", 409);
        }

        var now = DateTime.UtcNow;

        // Write SlugClaimed to the slug stream
        var slugClaimed = new SlugClaimed(command.HouseholdId, slug, now);
        await store.AppendToStreamAsync(slugStreamId, slugClaimed, ExpectedVersion.Any);

        // Write HouseholdSlugSet to the household stream
        var slugSet = new HouseholdSlugSet(command.HouseholdId, slug, now);
        await store.AppendToStreamAsync(householdStreamId, slugSet, ExpectedVersion.Any);

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
