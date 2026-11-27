using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using FileEventStore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ChoreMonkey.Core.Feature.DisplayLinks;

/// <summary>Where the public pages and the household's web page live. Settings: Display:PublicBaseUrl, Display:HouseholdBaseUrl.</summary>
public sealed class DisplaySettings(IConfiguration config)
{
    public string PublicBaseUrl => (config["Display:PublicBaseUrl"] is { Length: > 0 } p ? p : "https://itsybitsylist-api.azurewebsites.net").TrimEnd('/');
    public string HouseholdBaseUrl => (config["Display:HouseholdBaseUrl"] is { Length: > 0 } h ? h : "https://choremonkey.itsybit.se").TrimEnd('/');

    public string LinkUrl(string token) => $"{PublicBaseUrl}/display/{token}";
}

internal static class DisplaySupport
{
    public const string NotSetUp = "Display links aren't set up here yet.";

    /// <summary>The household's events and its chores' events together, as the views take them.</summary>
    public static async Task<List<object>> LoadHistoryAsync(IEventStore store, Guid householdId) =>
        (await store.FetchEventsAsync(HouseholdAggregate.StreamId(householdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(householdId))).Cast<object>())
            .ToList();

    public enum Who { Nobody, NotAdmin, Admin }

    /// <summary>
    /// Whether the caller is the household's admin: its session cookie says so, or X-Pin-Code is the admin PIN.
    /// Nobody means no credentials were offered at all (answered 401); NotAdmin means they were wrong (403, from Decide).
    /// </summary>
    public static Who CheckAdmin(HttpContext http, IHouseholdPrincipalAccessor principal, IReadOnlyList<object> history, Guid householdId)
    {
        var session = principal.Current;
        if (session is { IsAdmin: true } && session.HouseholdId == householdId) return Who.Admin;

        var header = http.Request.Headers["X-Pin-Code"].FirstOrDefault();
        if (string.IsNullOrEmpty(header)) return Who.Nobody;
        return int.TryParse(header, out var pin) && HouseholdPins.AdminPinMatches(history, pin) ? Who.Admin : Who.NotAdmin;
    }
}
