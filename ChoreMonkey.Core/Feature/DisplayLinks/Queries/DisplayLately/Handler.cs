using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLately;

internal class Handler(IEventStore store)
{
    public async Task<DisplayLatelyResponse?> HandleAsync(Guid householdId)
    {
        var history = await DisplaySupport.LoadHistoryAsync(store, householdId);
        return DisplayLatelyView.Project(history, DisplayClock.LocalNow(DateTime.UtcNow));
    }
}

/// <summary>The household's own screen: the same view a display link shows, for any member, no link needed.</summary>
internal static class DisplayLatelyEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/display/lately", async (Guid householdId, Handler handler) =>
        {
            var result = await handler.HandleAsync(householdId);
            return result == null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
