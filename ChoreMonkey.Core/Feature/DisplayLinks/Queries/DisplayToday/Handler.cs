using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayToday;

internal class Handler(IEventStore store, DisplaySettings settings)
{
    public async Task<DisplayTodayResponse?> HandleAsync(Guid householdId)
    {
        var history = await DisplaySupport.LoadHistoryAsync(store, householdId);
        return DisplayTodayView.Project(history, DisplayClock.LocalNow(DateTime.UtcNow), settings.HouseholdBaseUrl);
    }
}

/// <summary>The household's own screen: the same view a display link shows, for any member, no link needed.</summary>
internal static class DisplayTodayEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/display/today", async (Guid householdId, Handler handler) =>
        {
            var result = await handler.HandleAsync(householdId);
            return result == null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
