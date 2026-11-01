using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Party;

public record GetPartyQuery(Guid HouseholdId);

public record HeroDto(
    Guid MemberId,
    string Nickname,
    string? Status,
    int Xp,
    int Level,
    string Title,
    int TotalCompletions,
    int CompletionsToday,
    int CompletionsThisWeek);

public record GetPartyResponse(List<HeroDto> Heroes);

internal class Handler(IEventStore store)
{
    public async Task<GetPartyResponse> HandleAsync(GetPartyQuery request)
    {
        var householdEvents = await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId));
        var choreEvents     = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var history = householdEvents.Cast<object>().Concat(choreEvents.Cast<object>()).ToList();
        return PartyView.Project(history, DateTime.UtcNow);
    }
}

internal static class PartyEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/party",
            async (Guid householdId, Handler handler) =>
            {
                var result = await handler.HandleAsync(new GetPartyQuery(householdId));
                return Results.Ok(result);
            });
    }
}
