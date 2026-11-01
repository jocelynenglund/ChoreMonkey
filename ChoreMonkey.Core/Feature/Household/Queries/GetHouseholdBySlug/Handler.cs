using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Household.Queries.GetHouseholdBySlug;

public record GetHouseholdBySlugQuery(string Slug);

public record GetHouseholdBySlugResponse(Guid HouseholdId, string Name, int MemberCount);

internal class Handler(IEventStore store)
{
    public async Task<GetHouseholdBySlugResponse?> HandleAsync(GetHouseholdBySlugQuery request)
    {
        var slugStreamId = $"household-slug-{request.Slug}";
        var history = (await store.FetchEventsAsync(slugStreamId)).Cast<object>().ToList();

        var claimed = history.OfType<SlugClaimed>().FirstOrDefault();
        if (claimed == null)
            return null;

        var householdStreamId = HouseholdAggregate.StreamId(claimed.HouseholdId);
        history.AddRange((await store.FetchEventsAsync(householdStreamId)).Cast<object>());

        return GetHouseholdBySlugView.Project(history);
    }
}

internal static class GetHouseholdBySlugEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("h/{slug}", async (string slug, Handler handler) =>
        {
            var query = new GetHouseholdBySlugQuery(slug);
            var result = await handler.HandleAsync(query);

            if (result == null)
                return Results.NotFound(new { error = "Household not found." });

            return Results.Ok(result);
        });
    }
}
