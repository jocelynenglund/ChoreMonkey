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
        var slugEvents = await store.FetchEventsAsync(slugStreamId);

        var claimed = slugEvents.OfType<SlugClaimed>().FirstOrDefault();
        if (claimed == null)
            return null;

        var householdStreamId = HouseholdAggregate.StreamId(claimed.HouseholdId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);

        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
            return null;

        var memberCount = householdEvents.OfType<MemberJoinedHousehold>().Select(e => e.MemberId).ToHashSet();
        var removedMembers = householdEvents.OfType<MemberRemoved>().Select(e => e.MemberId).ToHashSet();
        var activeMemberCount = memberCount.Except(removedMembers).Count();

        return new GetHouseholdBySlugResponse(claimed.HouseholdId, householdCreated.Name, activeMemberCount);
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
